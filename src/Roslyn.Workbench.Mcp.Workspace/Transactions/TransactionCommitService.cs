using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class TransactionCommitService : ITransactionCommitService
{
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IWorkspaceCommitWriter _commitWriter;
    private readonly IWorkspaceCommitPlanner _commitPlanner;
    private readonly IWorkspaceCommitLockManager _commitLockManager;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    public TransactionCommitService(
        IWorkspaceSessionStore sessionStore,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceCommitWriter commitWriter,
        IWorkspaceCommitPlanner commitPlanner,
        IWorkspaceCommitLockManager commitLockManager,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher)
    {
        _sessionStore = sessionStore;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
        _recoveryStore = recoveryStore;
        _commitWriter = commitWriter;
        _commitPlanner = commitPlanner;
        _commitLockManager = commitLockManager;
        _instanceStatusPublisher = instanceStatusPublisher;
    }

    public async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        WorkspaceSelection selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before committing changes.",
                RequiredAction.StartTransaction);
        }

        WorkspaceOperationResult<TransactionCommitOutcome>? validationFailure;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            WorkbenchPerformanceEventSource.TransactionCommitOperation,
            WorkbenchPerformanceEventSource.CommitValidationPhase))
        {
            validationFailure = ValidateCommit(session, transaction, expectedSnapshot, cancellationToken);
        }

        if (validationFailure is not null)
        {
            return validationFailure;
        }

        using var applicationCertification = _workspaceChangeDetector.BeginCertification(
            session.Workspace.WorkspaceRoot);
        var context = WorkspaceOperationContextFactory.Create(session);
        WorkspaceCommitLockAcquisition lockAcquisition;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            WorkbenchPerformanceEventSource.TransactionCommitOperation,
            WorkbenchPerformanceEventSource.CommitLockAcquisitionPhase))
        {
            lockAcquisition = _commitLockManager.Acquire(session.Workspace.WorkspaceRoot);
        }

        if (lockAcquisition.IsContended)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "Another server instance is committing this workspace.",
                RequiredAction.Retry);
        }

        if (lockAcquisition.IsFailed)
        {
            return _resultFactory.Faulted<TransactionCommitOutcome>(
                "CommitLockFailed",
                lockAcquisition.ErrorMessage,
                RequiredAction.Retry,
                context);
        }

        using var commitLock = lockAcquisition.Lock;

        var result = await CommitUnderLockAsync(
            session,
            transaction,
            context,
            applicationCertification,
            cancellationToken);
        return result;
    }

    private WorkspaceOperationResult<TransactionCommitOutcome>? ValidateCommit(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        var context = WorkspaceOperationContextFactory.Create(session);
        var snapshotValidation = _snapshotGuard.Validate(session, expectedSnapshot);
        if (!snapshotValidation.IsValid)
        {
            return _resultFactory.Conflict<TransactionCommitOutcome>(snapshotValidation.Error, context);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Conflict<TransactionCommitOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "Roll back the conflicted transaction before committing changes.",
                RequiredAction.RollbackTransaction,
                context);
        }

        if (transaction.CurrentRevision == 0)
        {
            var outcome = new TransactionCommitOutcome
            {
                Committed = false,
                Transaction = transaction.ToInfo(conflicted: false),
            };

            return _resultFactory.NoChange(context, outcome);
        }

        if (!_workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            return null;
        }

        var conflictedSession = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
        _sessionStore.ReplaceSession(conflictedSession);
        _instanceStatusPublisher.QueueUpdate(
            conflictedSession.Workspace.WorkspaceId,
            conflictedSession.State,
            conflictedSession.Transaction?.CurrentRevision,
            commitId: null,
            commitPhase: null);

        var conflictedContext = WorkspaceOperationContextFactory.Create(conflictedSession);

        return _resultFactory.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            "The transaction conflicted with external workspace changes.",
            RequiredAction.RollbackTransaction,
            conflictedContext);
    }

    private async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitUnderLockAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        WorkspaceOperationContext context,
        IWorkspaceInputCertification applicationCertification,
        CancellationToken cancellationToken)
    {
        var commitId = Guid.NewGuid().ToString("n");
        WorkspaceCommitManifest? manifest = null;
        var applicationStarted = false;
        try
        {
            WorkspaceCommitPlanResult planningResult;
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                WorkbenchPerformanceEventSource.TransactionCommitOperation,
                WorkbenchPerformanceEventSource.CommitPlanningPhase))
            {
                planningResult = await CreateCommitPlanAsync(
                    session,
                    transaction,
                    commitId,
                    cancellationToken);
            }

            if (!planningResult.IsSucceeded)
            {
                return await TransitionCommitConflictAsync(
                    session,
                    transaction,
                    planningResult.ErrorMessage);
            }

            var plan = planningResult.Plan;
            manifest = plan.Manifest;
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                WorkbenchPerformanceEventSource.TransactionCommitOperation,
                WorkbenchPerformanceEventSource.CommitPlanPersistencePhase))
            {
                var persistence = await StageCommitAsync(
                    session,
                    transaction,
                    commitId,
                    plan,
                    cancellationToken);

                if (!persistence.IsPersisted)
                {
                    await ClearCommitPhaseAsync(session, transaction.CurrentRevision);
                    var message = CreateRecoveryCapacityMessage(persistence.ErrorMessage);
                    return _resultFactory.Rejected<TransactionCommitOutcome>(
                        WorkspaceErrorCodes.CommitRecoveryCapacity,
                        message,
                        RequiredAction.RollbackTransaction,
                        context);
                }
            }

            WorkspaceCommitValidationResult revalidation;
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                WorkbenchPerformanceEventSource.TransactionCommitOperation,
                WorkbenchPerformanceEventSource.CommitRevalidationPhase))
            {
                revalidation = await _commitWriter.RevalidateAsync(plan.Manifest, cancellationToken);
            }

            if (!revalidation.IsValid)
            {
                _recoveryStore.DeleteStatus(commitId);
                return await TransitionCommitConflictAsync(
                    session,
                    transaction,
                    revalidation.ErrorMessage);
            }

            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                WorkbenchPerformanceEventSource.TransactionCommitOperation,
                WorkbenchPerformanceEventSource.CommitApplyingPersistencePhase))
            {
                manifest = await BeginApplyingAsync(
                    session,
                    transaction,
                    commitId,
                    plan.Manifest,
                    cancellationToken);
            }

            applicationStarted = true;

            WorkspaceCommitValidationResult application;
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                WorkbenchPerformanceEventSource.TransactionCommitOperation,
                WorkbenchPerformanceEventSource.CommitApplicationPhase))
            {
                application = await _commitWriter.ApplyAsync(manifest);
            }

            if (!application.IsValid)
            {
                return await RecoverFailedCommitAsync(
                    session,
                    transaction,
                    context,
                    commitId,
                    manifest,
                    applicationStarted: true,
                    failureMessage: application.ErrorMessage,
                    validationConflict: true);
            }

            using var promotionCertification = _workspaceChangeDetector.BeginCertification(
                session.Workspace.WorkspaceRoot);
            using var applicationInputManifest = applicationCertification.Complete(
                session.InputManifest,
                GetCommitOwnedPaths(manifest));
            var appliedState = await _commitWriter.ValidateAppliedStateAsync(manifest);
            if (!appliedState.IsValid)
            {
                return await RecoverFailedCommitAsync(
                    session,
                    transaction,
                    context,
                    commitId,
                    manifest,
                    applicationStarted: true,
                    failureMessage: appliedState.ErrorMessage,
                    validationConflict: true);
            }

            var inputManifest = _workspaceChangeDetector.BuildManifest(
                transaction.CurrentSolution,
                session.Workspace.LoadedPath,
                session.Workspace.WorkspaceRoot,
                promotionCertification,
                session.MsBuildProperties,
                CancellationToken.None);

            WorkspaceSessionSnapshot committedSession;
            var inputManifestHandedOff = false;
            try
            {
                var inputChangeFailureMessage = DetectInputChangeFailureMessage(
                    applicationInputManifest,
                    inputManifest);

                if (inputChangeFailureMessage is not null)
                {
                    return await RecoverFailedCommitAsync(
                        session,
                        transaction,
                        context,
                        commitId,
                        manifest,
                        applicationStarted: true,
                        failureMessage: inputChangeFailureMessage,
                        validationConflict: true);
                }

                appliedState = await _commitWriter.ValidateAppliedStateAsync(manifest);
                if (!appliedState.IsValid)
                {
                    return await RecoverFailedCommitAsync(
                        session,
                        transaction,
                        context,
                        commitId,
                        manifest,
                        applicationStarted: true,
                        failureMessage: appliedState.ErrorMessage,
                        validationConflict: true);
                }

                inputChangeFailureMessage = DetectInputChangeFailureMessage(
                    applicationInputManifest,
                    inputManifest);

                if (inputChangeFailureMessage is not null)
                {
                    return await RecoverFailedCommitAsync(
                        session,
                        transaction,
                        context,
                        commitId,
                        manifest,
                        applicationStarted: true,
                        failureMessage: inputChangeFailureMessage,
                        validationConflict: true);
                }

                using (WorkbenchPerformanceEventSource.Log.StartPhase(
                    WorkbenchPerformanceEventSource.TransactionCommitOperation,
                    WorkbenchPerformanceEventSource.CommitWorkspacePromotionPhase))
                {
                    committedSession = CreateCommittedSession(session, transaction, inputManifest);
                }

                inputManifestHandedOff = true;
            }
            finally
            {
                if (!inputManifestHandedOff)
                {
                    inputManifest.Dispose();
                }
            }

            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                WorkbenchPerformanceEventSource.TransactionCommitOperation,
                WorkbenchPerformanceEventSource.CommitCleanupPhase))
            {
                var completion = await CompleteCommitAsync(
                    session.InputManifest,
                    committedSession,
                    manifest);

                if (!completion.IsCompleted)
                {
                    return await RecoverFailedCommitAsync(
                        session,
                        transaction,
                        context,
                        commitId,
                        manifest,
                        applicationStarted: true,
                        failureMessage: completion.Failure.Message,
                        validationConflict: false,
                        restoredRequiredAction: null);
                }
            }

            var outcome = new TransactionCommitOutcome
            {
                Committed = true,
            };

            var committedContext = WorkspaceOperationContextFactory.Create(committedSession);

            return _resultFactory.Succeeded(outcome, committedContext);
        }
        catch (OperationCanceledException) when (!applicationStarted)
        {
            await RestoreCancelledCommitAsync(manifest);
            throw;
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            return await RecoverFailedCommitAsync(
                session,
                transaction,
                context,
                commitId,
                manifest,
                applicationStarted,
                CreateFileSystemFailureMessage(exception),
                validationConflict: false);
        }
    }

    private ValueTask<WorkspaceCommitPlanResult> CreateCommitPlanAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        string commitId,
        CancellationToken cancellationToken)
    {
        return _commitPlanner.CreateAsync(
            commitId,
            session.Workspace.LoadedPath,
            session.Workspace.WorkspaceRoot,
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            cancellationToken);
    }

    private async ValueTask<CommitRecoveryPlanPersistenceResult> StageCommitAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        string commitId,
        WorkspaceCommitPlan plan,
        CancellationToken cancellationToken)
    {
        await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, "Staging");
        return await _recoveryStore.PersistPlanAsync(plan, cancellationToken);
    }

    private async ValueTask<WorkspaceCommitManifest> BeginApplyingAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        string commitId,
        WorkspaceCommitManifest manifest,
        CancellationToken cancellationToken)
    {
        var applyingManifest = manifest with { State = RecoveryState.Applying };
        await _recoveryStore.WriteManifestAsync(applyingManifest, cancellationToken);
        await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, "Applying");
        return applyingManifest;
    }

    private WorkspaceSessionSnapshot CreateCommittedSession(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        WorkspaceInputManifest inputManifest)
    {
        var loadDiagnostics = session.LoadDiagnostics;
        if (!inputManifest.IsComplete)
        {
            var inputEvaluationDiagnostics = WorkspaceInputEvaluationDiagnostics.Create(
                inputManifest.EvaluationFailures);

            loadDiagnostics = [.. loadDiagnostics, .. inputEvaluationDiagnostics];
        }

        var committedSnapshotId = _sessionStore.AllocateWorkspaceSnapshotId();
        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            session.Workspace,
            committedSnapshotId,
            transaction: null);

        var committedSession = session with
        {
            CommittedSnapshotId = committedSnapshotId,
            Transaction = null,
            CurrentSolution = transaction.CurrentSolution,
            InputManifest = inputManifest,
            LoadDiagnostics = loadDiagnostics,
            State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionCommitted),
            CurrentSnapshotIdentity = snapshotIdentity,
        };

        if (inputManifest.IsComplete)
        {
            return committedSession;
        }

        return _workspaceStateTransitions.ApplyExternalChangeDetected(committedSession);
    }

    private string? DetectInputChangeFailureMessage(
        WorkspaceInputManifest applicationInputManifest,
        WorkspaceInputManifest promotionInputManifest)
    {
        var applicationInputsChanged = _workspaceChangeDetector.HasChanged(
            applicationInputManifest,
            CancellationToken.None);

        if (applicationInputsChanged)
        {
            return CreateInputChangeFailureMessage(
                "Application",
                applicationInputManifest.Change);
        }

        if (!promotionInputManifest.IsComplete)
        {
            return null;
        }

        var promotionInputsChanged = _workspaceChangeDetector.HasChanged(
            promotionInputManifest,
            CancellationToken.None);

        if (!promotionInputsChanged)
        {
            return null;
        }

        return CreateInputChangeFailureMessage("Promotion", promotionInputManifest.Change);
    }

    private static string CreateInputChangeFailureMessage(
        string certification,
        WorkspaceInputChange? change)
    {
        var message = new StringBuilder();
        message.Append("Workspace inputs changed during commit promotion. Certification: ");
        message.Append(certification);
        message.Append('.');
        if (change is null)
        {
            message.Append(" Detection details were unavailable.");
            return message.ToString();
        }

        message.Append(" Detection source: ");
        message.Append(change.DetectionSource);
        message.Append(". Change kind: ");
        message.Append(change.Kind);
        message.Append('.');
        if (change.ErrorCode is { } errorCode)
        {
            message.Append(" Error code: ");
            message.Append(errorCode);
            message.Append('.');
        }

        if (change.Path is { } path)
        {
            message.Append(" Path: ");
            message.Append(path);
            message.Append('.');
        }

        if (change.PreviousPath is { } previousPath)
        {
            message.Append(" Previous path: ");
            message.Append(previousPath);
            message.Append('.');
        }

        return message.ToString();
    }

    private async ValueTask<TransactionCompletionResult> CompleteCommitAsync(
        WorkspaceInputManifest previousInputManifest,
        WorkspaceSessionSnapshot committedSession,
        WorkspaceCommitManifest applyingManifest)
    {
        var committedManifest = applyingManifest with { State = RecoveryState.Committed };
        var sessionReplaced = false;
        try
        {
            await _recoveryStore.WriteManifestAsync(committedManifest, CancellationToken.None);

            var completion = _sessionStore.TryCompleteTransaction(committedSession);
            if (!completion.IsCompleted)
            {
                committedSession.InputManifest.Dispose();
                return completion;
            }

            sessionReplaced = true;
            previousInputManifest.Dispose();
            var recoveryArtifactsRemoved = await _commitWriter.CompleteAsync(committedManifest);
            if (recoveryArtifactsRemoved)
            {
                _recoveryStore.DeleteStatus(committedManifest.CommitId);
            }

            await PublishCommitPhaseAsync(committedSession, null, null, "Committed");
            return completion;
        }
        catch
        {
            if (!sessionReplaced)
            {
                committedSession.InputManifest.Dispose();
            }

            throw;
        }
    }

    private async ValueTask RestoreCancelledCommitAsync(WorkspaceCommitManifest? manifest)
    {
        if (manifest is null)
        {
            return;
        }

        var state = await _commitWriter.RestoreAsync(manifest);
        await TryWriteManifestAsync(manifest with { State = state });
        if (state == RecoveryState.Restored)
        {
            _recoveryStore.DeleteStatus(manifest.CommitId);
        }
    }

    private async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> RecoverFailedCommitAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        WorkspaceOperationContext context,
        string commitId,
        WorkspaceCommitManifest? manifest,
        bool applicationStarted,
        string failureMessage,
        bool validationConflict,
        RequiredAction? restoredRequiredAction = RequiredAction.Retry)
    {
        using var recoveryPhase = WorkbenchPerformanceEventSource.Log.StartPhase(
            WorkbenchPerformanceEventSource.TransactionCommitOperation,
            WorkbenchPerformanceEventSource.CommitRecoveryPhase);

        var state = RecoveryState.RecoveryIncomplete;
        if (manifest is not null)
        {
            state = await _commitWriter.RestoreAsync(manifest);
        }

        var recoveryStatePersisted = true;
        if (manifest is not null)
        {
            await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, "Restoring");
            var recoveredManifest = manifest with { State = state, Message = failureMessage };
            recoveryStatePersisted = await TryWriteManifestAsync(recoveredManifest);
            if (state == RecoveryState.Restored)
            {
                _recoveryStore.DeleteStatus(manifest.CommitId);
            }

            await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, state.ToString());
        }

        if (validationConflict && state == RecoveryState.Restored)
        {
            return await TransitionCommitConflictAsync(
                session,
                transaction,
                failureMessage);
        }

        var errorCode = "CommitPreparationFailed";
        if (applicationStarted)
        {
            errorCode = "CommitFailed";
        }

        var errorMessage = CreateCommitFailureMessage(
            applicationStarted,
            recoveryStatePersisted,
            failureMessage);

        RequiredAction? requiredAction = RequiredAction.ResolveRecovery;
        if (!applicationStarted)
        {
            requiredAction = RequiredAction.Retry;
        }
        else if (state == RecoveryState.Restored)
        {
            requiredAction = restoredRequiredAction;
        }

        return _resultFactory.Faulted<TransactionCommitOutcome>(
            errorCode,
            errorMessage,
            requiredAction,
            context);
    }

    private async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> TransitionCommitConflictAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        string failureMessage)
    {
        var conflictedSession = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
        _sessionStore.ReplaceSession(conflictedSession);
        await PublishCommitPhaseAsync(
            conflictedSession,
            transaction.CurrentRevision,
            commitId: null,
            commitPhase: WorkspaceLifecycleState.TransactionConflicted.ToString());

        var context = WorkspaceOperationContextFactory.Create(conflictedSession);

        return _resultFactory.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            failureMessage,
            RequiredAction.RollbackTransaction,
            context);
    }

    private ValueTask PublishCommitPhaseAsync(
        WorkspaceSessionSnapshot session,
        long? transactionRevision,
        string? commitId,
        string commitPhase)
    {
        return _instanceStatusPublisher.UpdateAsync(
            session.Workspace.WorkspaceId,
            session.State,
            transactionRevision,
            commitId,
            commitPhase);
    }

    private ValueTask ClearCommitPhaseAsync(
        WorkspaceSessionSnapshot session,
        long transactionRevision)
    {
        return _instanceStatusPublisher.UpdateAsync(
            session.Workspace.WorkspaceId,
            session.State,
            transactionRevision,
            commitId: null,
            commitPhase: null);
    }

    private async ValueTask<bool> TryWriteManifestAsync(WorkspaceCommitManifest manifest)
    {
        try
        {
            await _recoveryStore.WriteManifestAsync(manifest, CancellationToken.None);
            return true;
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            return false;
        }
    }

    private static List<string> GetCommitOwnedPaths(WorkspaceCommitManifest manifest)
    {
        var maximumPathCount = manifest.Entries.Count * 4 + manifest.CreatedDirectories.Count;
        var paths = new List<string>(maximumPathCount);
        paths.AddRange(manifest.CreatedDirectories);
        foreach (var entry in manifest.Entries)
        {
            paths.Add(entry.TargetPath);
            AddPathIfPresent(paths, entry.BackupPath);
            AddPathIfPresent(paths, entry.StagedPath);
            AddPathIfPresent(paths, entry.DeleteMarkerPath);
        }

        return paths;
    }

    private static void AddPathIfPresent(List<string> paths, string? path)
    {
        if (path is not null)
        {
            paths.Add(path);
        }
    }

    private static string CreateCommitFailureMessage(
        bool applicationStarted,
        bool recoveryStatePersisted,
        string failureMessage)
    {
        var message = applicationStarted
            ? "The transaction commit failed and its changes were restored or retained for recovery."
            : "The transaction commit could not update its recovery record and no workspace changes were applied.";

        var detailedMessage = $"{message} Failure: {failureMessage}";

        return recoveryStatePersisted
            ? detailedMessage
            : $"{detailedMessage} The final recovery state could not be persisted; any retained recovery record may report an earlier phase.";
    }

    private static string CreateFileSystemFailureMessage(Exception exception)
    {
        var underlyingException = exception.GetBaseException();
        return ReferenceEquals(exception, underlyingException)
            ? exception.Message
            : $"{exception.Message} {underlyingException.Message}";
    }

    private static string CreateRecoveryCapacityMessage(string capacityMessage)
    {
        return $"The transaction cannot be committed because its recovery data exceeds a supported size limit. Roll back this transaction and stage a smaller change. {capacityMessage}";
    }

    private static bool IsRecoverableFileSystemException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

}
