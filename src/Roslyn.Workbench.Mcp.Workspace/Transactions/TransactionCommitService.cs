using System.Text;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Persists active transactions through the durable commit and recovery pipeline.
/// </summary>
internal sealed class TransactionCommitService : ITransactionCommitService
{
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceAuthority _workspaceAuthority;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IWorkspaceCommitWriter _commitWriter;
    private readonly IWorkspaceCommitPlanner _commitPlanner;
    private readonly IWorkspaceCommitLockManager _commitLockManager;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;
    private readonly WorkspaceOptions _options;
    private readonly ITransactionReviewDocumentFactory? _reviewDocumentFactory;
    private readonly ITransactionReviewIdentityService? _reviewIdentityService;
    private readonly ITransactionCompilerValidationService? _compilerValidationService;

    private ITransactionReviewDocumentFactory ReviewDocumentFactory => _reviewDocumentFactory
        ?? throw new InvalidOperationException("Transaction review services must be composed when receipt authorisation is required.");

    private ITransactionReviewIdentityService ReviewIdentityService => _reviewIdentityService
        ?? throw new InvalidOperationException("Transaction review services must be composed when receipt authorisation is required.");

    private ITransactionCompilerValidationService CompilerValidationService => _compilerValidationService
        ?? throw new InvalidOperationException("Compiler validation services must be composed when compiler validation is required.");

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCommitService"/> class.
    /// </summary>
    /// <param name="sessionStore">The store that publishes committed session state.</param>
    /// <param name="workspaceAuthority">The Host-owned authority revalidated before commit.</param>
    /// <param name="workspaceChangeDetector">The component that detects external changes before commit.</param>
    /// <param name="workspaceStateTransitions">The coordinator that applies workspace lifecycle state changes.</param>
    /// <param name="snapshotGuard">The guard that rejects operations targeting a stale transaction snapshot.</param>
    /// <param name="resultFactory">The factory used to create protocol result payloads.</param>
    /// <param name="recoveryStore">The store that persists the recovery plan before file changes begin.</param>
    /// <param name="commitWriter">The component that applies and validates planned file operations.</param>
    /// <param name="commitPlanner">The planner that converts a transaction into atomic file operations.</param>
    /// <param name="commitLockManager">The manager that acquires exclusive workspace commit locks.</param>
    /// <param name="instanceStatusPublisher">The publisher that keeps the workspace instance record current.</param>
    /// <param name="options">The Workspace policy controlling receipt-authorisation enforcement.</param>
    /// <param name="compilerValidationService">The optional compiler-impact validator composed by Host policy.</param>
    public TransactionCommitService(
        IWorkspaceSessionStore sessionStore,
        IWorkspaceAuthority workspaceAuthority,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceCommitWriter commitWriter,
        IWorkspaceCommitPlanner commitPlanner,
        IWorkspaceCommitLockManager commitLockManager,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher,
        IOptions<WorkspaceOptions> options,
        ITransactionCompilerValidationService? compilerValidationService = null)
    {
        _sessionStore = sessionStore;
        _workspaceAuthority = workspaceAuthority;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
        _recoveryStore = recoveryStore;
        _commitWriter = commitWriter;
        _commitPlanner = commitPlanner;
        _commitLockManager = commitLockManager;
        _instanceStatusPublisher = instanceStatusPublisher;
        _options = options.Value;
        _compilerValidationService = compilerValidationService;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCommitService"/> class.
    /// </summary>
    /// <param name="sessionStore">The store that publishes committed session state.</param>
    /// <param name="workspaceAuthority">The Host-owned authority revalidated before commit.</param>
    /// <param name="workspaceChangeDetector">The component that detects external changes before commit.</param>
    /// <param name="workspaceStateTransitions">The coordinator that applies workspace lifecycle state changes.</param>
    /// <param name="snapshotGuard">The guard that rejects operations targeting a stale transaction snapshot.</param>
    /// <param name="resultFactory">The factory used to create protocol result payloads.</param>
    /// <param name="recoveryStore">The store that persists the recovery plan before file changes begin.</param>
    /// <param name="commitWriter">The component that applies and validates planned file operations.</param>
    /// <param name="commitPlanner">The planner that converts a transaction into atomic file operations.</param>
    /// <param name="commitLockManager">The manager that acquires exclusive workspace commit locks.</param>
    /// <param name="instanceStatusPublisher">The publisher that keeps the workspace instance record current.</param>
    /// <param name="options">The Workspace policy controlling receipt-authorisation enforcement.</param>
    /// <param name="reviewDocumentFactory">The factory that recreates canonical receipt entries from the final commit plan.</param>
    /// <param name="reviewIdentityService">The service that recalculates the canonical receipt identity.</param>
    /// <param name="compilerValidationService">The optional compiler-impact validator composed by Host policy.</param>
    public TransactionCommitService(
        IWorkspaceSessionStore sessionStore,
        IWorkspaceAuthority workspaceAuthority,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ICommitRecoveryStore recoveryStore,
        IWorkspaceCommitWriter commitWriter,
        IWorkspaceCommitPlanner commitPlanner,
        IWorkspaceCommitLockManager commitLockManager,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher,
        IOptions<WorkspaceOptions> options,
        ITransactionReviewDocumentFactory reviewDocumentFactory,
        ITransactionReviewIdentityService reviewIdentityService,
        ITransactionCompilerValidationService? compilerValidationService = null)
        : this(
            sessionStore,
            workspaceAuthority,
            workspaceChangeDetector,
            workspaceStateTransitions,
            snapshotGuard,
            resultFactory,
            recoveryStore,
            commitWriter,
            commitPlanner,
            commitLockManager,
            instanceStatusPublisher,
            options,
            compilerValidationService)
    {
        _reviewDocumentFactory = reviewDocumentFactory;
        _reviewIdentityService = reviewIdentityService;
    }

    /// <summary>
    /// Commits the selected transaction to the workspace files.
    /// </summary>
    /// <param name="selection">The resolved workspace selection on which the operation runs.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        WorkspaceSelection selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        return CommitAsync(
            selection,
            expectedSnapshot,
            receiptAuthorisation: null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        WorkspaceSelection selection,
        SnapshotPrecondition? expectedSnapshot,
        TransactionReceiptAuthorisation? receiptAuthorisation,
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

        if (!_workspaceAuthority.IsWorkspaceAllowed(
            session.Workspace.LoadedPath,
            session.Workspace.WorkspaceRoot))
        {
            var authorityContext = WorkspaceOperationContextFactory.Create(session);
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.WorkspaceAuthorityChanged,
                "The Workspace path or effective root no longer complies with the Host's configured authority. Restore the original directory topology or roll back the transaction.",
                RequiredAction.RollbackTransaction,
                authorityContext);
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

        var receiptBindingFailure = ValidateReceiptBinding(session, transaction, receiptAuthorisation);
        if (receiptBindingFailure is not null)
        {
            return receiptBindingFailure;
        }

        if (_options.CompilerValidationRequired)
        {
            var compilerValidation = await CompilerValidationService.ValidateAsync(
                session,
                cancellationToken);

            if (!compilerValidation.Succeeded)
            {
                return CreateCompilerValidationFailure(session, compilerValidation);
            }
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
            receiptAuthorisation,
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

    private WorkspaceOperationResult<TransactionCommitOutcome> CreateCompilerValidationFailure(
        WorkspaceSessionSnapshot session,
        TransactionCompilerValidationOutcome validation)
    {
        var context = WorkspaceOperationContextFactory.Create(session);
        var warnings = validation.Limitations
            .Select(static limitation => new WarningInfo
            {
                Code = WorkspaceErrorCodes.CompilerValidationIncomplete,
                Message = limitation,
            })
            .ToArray();

        if (!validation.IsComplete)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.CompilerValidationIncomplete,
                "Compiler validation could not evaluate every affected loaded project. No files were persisted. Roll back the transaction, reload the Workspace, and start a new transaction before retrying.",
                validation.RecoveryAction,
                context,
                validation.IntroducedDiagnostics,
                warnings);
        }

        return _resultFactory.Rejected<TransactionCommitOutcome>(
            WorkspaceErrorCodes.NewCompilerErrors,
            $"The transaction introduced {validation.IntroducedErrorCount} compiler error(s). No files were persisted and the transaction remains active.",
            context: context,
            diagnostics: validation.IntroducedDiagnostics,
            warnings: warnings);
    }

    private async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitUnderLockAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        WorkspaceOperationContext context,
        IWorkspaceInputCertification applicationCertification,
        TransactionReceiptAuthorisation? receiptAuthorisation,
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

            var receiptValidationFailure = await ValidateReceiptIdentityAsync(
                session,
                plan,
                receiptAuthorisation,
                context,
                cancellationToken);

            if (receiptValidationFailure is not null)
            {
                return receiptValidationFailure;
            }

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

    private WorkspaceOperationResult<TransactionCommitOutcome>? ValidateReceiptBinding(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        TransactionReceiptAuthorisation? receiptAuthorisation)
    {
        if (!_options.ReceiptAuthorisationRequired)
        {
            return null;
        }

        var context = WorkspaceOperationContextFactory.Create(session);
        if (receiptAuthorisation is null)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.TransactionReceiptRequired,
                "Review the current transaction and approve its receipt before committing changes.",
                RequiredAction.ReviewTransaction,
                context);
        }

        var matches = ReviewIdentityService.IsBoundTo(
            receiptAuthorisation.Identity,
            session,
            transaction);

        if (matches)
        {
            return null;
        }

        return _resultFactory.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionReceiptMismatch,
            "The approved receipt does not identify the current transaction state. Review the transaction again before committing.",
            RequiredAction.ReviewTransaction,
            context);
    }

    private async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>?> ValidateReceiptIdentityAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceCommitPlan plan,
        TransactionReceiptAuthorisation? receiptAuthorisation,
        WorkspaceOperationContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.ReceiptAuthorisationRequired)
        {
            return null;
        }

        if (receiptAuthorisation is null)
        {
            throw new InvalidOperationException("Receipt binding validation must run before exact identity validation.");
        }

        var documents = await ReviewDocumentFactory.CreateAsync(
            session,
            plan,
            changes: null,
            cancellationToken);

        var currentIdentity = ReviewIdentityService.Create(session, documents);
        if (currentIdentity == receiptAuthorisation.Identity)
        {
            return null;
        }

        return _resultFactory.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionReceiptMismatch,
            "The staged persistence set no longer matches the approved receipt. Review the transaction again before committing.",
            RequiredAction.ReviewTransaction,
            context);
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
