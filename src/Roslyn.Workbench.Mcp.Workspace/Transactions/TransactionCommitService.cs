using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

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

        var validationFailure = ValidateCommit(session, transaction, expectedSnapshot, cancellationToken);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var context = CreateContext(session);
        var lockAcquisition = _commitLockManager.Acquire(session.Workspace.WorkspaceRoot);
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

        var result = await CommitUnderLockAsync(session, transaction, context, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private WorkspaceOperationResult<TransactionCommitOutcome>? ValidateCommit(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        var context = CreateContext(session);
        var snapshotMismatch = _snapshotGuard.Validate(session, expectedSnapshot);
        if (snapshotMismatch is not null)
        {
            return _resultFactory.Conflict<TransactionCommitOutcome>(snapshotMismatch, context);
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
            return _resultFactory.NoChange(
                context,
                new TransactionCommitOutcome
                {
                    Committed = false,
                    Transaction = transaction.ToInfo(conflicted: false),
                });
        }

        if (!_workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            return null;
        }

        var conflictedSession = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
        _sessionStore.ReplaceSession(conflictedSession);
        return _resultFactory.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            "The transaction conflicted with external workspace changes.",
            RequiredAction.RollbackTransaction,
            CreateContext(conflictedSession));
    }

    private async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitUnderLockAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        WorkspaceOperationContext context,
        CancellationToken cancellationToken)
    {
        var commitId = Guid.NewGuid().ToString("n");
        WorkspaceCommitManifest? manifest = null;
        var applicationStarted = false;
        try
        {
            var planningResult = await CreateCommitPlanAsync(
                session,
                transaction,
                commitId,
                cancellationToken).ConfigureAwait(false);
            if (!planningResult.IsSucceeded)
            {
                return await TransitionCommitConflictAsync(session, transaction).ConfigureAwait(false);
            }

            var plan = planningResult.Plan;
            manifest = plan.Manifest;
            await StageCommitAsync(session, transaction, commitId, plan, cancellationToken).ConfigureAwait(false);

            var revalidation = await _commitWriter.RevalidateAsync(plan.Manifest, cancellationToken).ConfigureAwait(false);
            if (!revalidation.IsValid)
            {
                _recoveryStore.DeleteStatus(commitId);
                return await TransitionCommitConflictAsync(session, transaction).ConfigureAwait(false);
            }

            manifest = await BeginApplyingAsync(session, transaction, commitId, plan.Manifest, cancellationToken).ConfigureAwait(false);
            applicationStarted = true;

            var application = await _commitWriter.ApplyAsync(manifest).ConfigureAwait(false);
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
                    validationConflict: true).ConfigureAwait(false);
            }

            var committedSession = CreateCommittedSession(session, transaction);
            await CompleteCommitAsync(committedSession, manifest).ConfigureAwait(false);

            return _resultFactory.Succeeded(
                new TransactionCommitOutcome
                {
                    Committed = true,
                },
                CreateContext(committedSession));
        }
        catch (OperationCanceledException) when (!applicationStarted)
        {
            await RestoreCancelledCommitAsync(manifest).ConfigureAwait(false);
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
                exception.Message,
                validationConflict: false).ConfigureAwait(false);
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

    private async ValueTask StageCommitAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        string commitId,
        WorkspaceCommitPlan plan,
        CancellationToken cancellationToken)
    {
        await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, "Staging").ConfigureAwait(false);
        await _recoveryStore.PersistPlanAsync(plan, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WorkspaceCommitManifest> BeginApplyingAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        string commitId,
        WorkspaceCommitManifest manifest,
        CancellationToken cancellationToken)
    {
        var applyingManifest = manifest with { State = RecoveryState.Applying };
        await _recoveryStore.WriteManifestAsync(applyingManifest, cancellationToken).ConfigureAwait(false);
        await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, "Applying").ConfigureAwait(false);
        return applyingManifest;
    }

    private WorkspaceSessionSnapshot CreateCommittedSession(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction)
    {
        var inputManifest = _workspaceChangeDetector.BuildManifest(
            transaction.CurrentSolution,
            session.Workspace.LoadedPath);
        var committedSession = session with
        {
            Transaction = null,
            CurrentSolution = transaction.CurrentSolution,
            InputManifest = inputManifest,
            LoadDiagnostics = inputManifest.IsComplete
                ? session.LoadDiagnostics
                : session.LoadDiagnostics.Concat(
                    WorkspaceInputEvaluationDiagnostics.Create(inputManifest.EvaluationFailures)).ToArray(),
            State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionCommitted),
        };

        return inputManifest.IsComplete
            ? committedSession
            : _workspaceStateTransitions.ApplyExternalChangeDetected(committedSession);
    }

    private async ValueTask CompleteCommitAsync(
        WorkspaceSessionSnapshot committedSession,
        WorkspaceCommitManifest applyingManifest)
    {
        var committedManifest = applyingManifest with { State = RecoveryState.Committed };
        await _recoveryStore.WriteManifestAsync(committedManifest, CancellationToken.None).ConfigureAwait(false);

        _sessionStore.ReplaceSessionAndSetTransactionOwner(committedSession, null);
        var recoveryArtifactsRemoved = await _commitWriter.CompleteAsync(committedManifest).ConfigureAwait(false);
        if (recoveryArtifactsRemoved)
        {
            _recoveryStore.DeleteStatus(committedManifest.CommitId);
        }

        await PublishCommitPhaseAsync(committedSession, null, null, "Committed").ConfigureAwait(false);
    }

    private async ValueTask RestoreCancelledCommitAsync(WorkspaceCommitManifest? manifest)
    {
        if (manifest is null)
        {
            return;
        }

        var state = await _commitWriter.RestoreAsync(manifest).ConfigureAwait(false);
        _ = await TryWriteManifestAsync(manifest with { State = state }).ConfigureAwait(false);
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
        bool validationConflict)
    {
        var state = manifest is null
            ? RecoveryState.RecoveryIncomplete
            : await _commitWriter.RestoreAsync(manifest).ConfigureAwait(false);
        var recoveryStatePersisted = true;
        if (manifest is not null)
        {
            await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, "Restoring").ConfigureAwait(false);
            var recoveredManifest = manifest with { State = state, Message = failureMessage };
            recoveryStatePersisted = await TryWriteManifestAsync(recoveredManifest).ConfigureAwait(false);
            if (state == RecoveryState.Restored)
            {
                _recoveryStore.DeleteStatus(manifest.CommitId);
            }

            await PublishCommitPhaseAsync(session, transaction.CurrentRevision, commitId, state.ToString()).ConfigureAwait(false);
        }

        if (validationConflict && state == RecoveryState.Restored)
        {
            return await TransitionCommitConflictAsync(session, transaction).ConfigureAwait(false);
        }

        return _resultFactory.Faulted<TransactionCommitOutcome>(
            applicationStarted ? "CommitFailed" : "CommitPreparationFailed",
            CreateCommitFailureMessage(applicationStarted, recoveryStatePersisted),
            !applicationStarted || state == RecoveryState.Restored ? RequiredAction.Retry : RequiredAction.ResolveRecovery,
            context);
    }

    private async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> TransitionCommitConflictAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction)
    {
        var conflictedSession = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
        _sessionStore.ReplaceSession(conflictedSession);
        await PublishCommitPhaseAsync(
            conflictedSession,
            transaction.CurrentRevision,
            commitId: null,
            commitPhase: WorkspaceLifecycleState.TransactionConflicted.ToString()).ConfigureAwait(false);
        return _resultFactory.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            "A commit target changed after the transaction was staged.",
            RequiredAction.RollbackTransaction,
            CreateContext(conflictedSession));
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

    private async ValueTask<bool> TryWriteManifestAsync(WorkspaceCommitManifest manifest)
    {
        try
        {
            await _recoveryStore.WriteManifestAsync(manifest, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            return false;
        }
    }

    private static string CreateCommitFailureMessage(bool applicationStarted, bool recoveryStatePersisted)
    {
        var message = applicationStarted
            ? "The transaction commit failed and its changes were restored or retained for recovery."
            : "The transaction commit could not update its recovery record and no workspace changes were applied.";
        return recoveryStatePersisted
            ? message
            : $"{message} The final recovery state could not be persisted; any retained recovery record may report an earlier phase.";
    }

    private static bool IsRecoverableFileSystemException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static WorkspaceOperationContext CreateContext(WorkspaceSessionSnapshot session)
    {
        return new WorkspaceOperationContext
        {
            WorkspaceId = session.Workspace.WorkspaceId,
            WorkspaceEpoch = session.Workspace.WorkspaceEpoch,
            TransactionRevision = session.Transaction?.CurrentRevision,
        };
    }
}
