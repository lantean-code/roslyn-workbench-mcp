using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Implements transaction start, preview, history navigation, commit, and rollback workflows.
/// </summary>
internal sealed class TransactionService : ITransactionService
{
    private readonly WorkspaceOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSessionAcquirer _sessionAcquirer;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ITransactionCommitService _transactionCommitService;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly IWorkspaceResolverFactory _resolverFactory;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionService"/> class.
    /// </summary>
    /// <param name="options">The configured transaction revision limit.</param>
    /// <param name="sessionStore">The store that publishes transactional session state.</param>
    /// <param name="sessionAcquirer">The component that acquires the workspace session used by a transaction operation.</param>
    /// <param name="workspaceStateTransitions">The coordinator that applies workspace lifecycle state changes.</param>
    /// <param name="snapshotGuard">The guard that rejects operations targeting a stale transaction snapshot.</param>
    /// <param name="resultFactory">The factory used to create protocol result payloads.</param>
    /// <param name="transactionCommitService">The service that provides transaction commit operations.</param>
    /// <param name="diffBuilder">The builder that calculates source differences between solution snapshots.</param>
    /// <param name="resolverFactory">The factory used to create the required resolver.</param>
    /// <param name="instanceStatusPublisher">The publisher that keeps the workspace instance record current.</param>
    public TransactionService(
        IOptions<WorkspaceOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSessionAcquirer sessionAcquirer,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ITransactionCommitService transactionCommitService,
        IWorkspaceDiffBuilder diffBuilder,
        IWorkspaceResolverFactory resolverFactory,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _sessionAcquirer = sessionAcquirer;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
        _transactionCommitService = transactionCommitService;
        _diffBuilder = diffBuilder;
        _resolverFactory = resolverFactory;
        _instanceStatusPublisher = instanceStatusPublisher;
    }

    /// <summary>
    /// Starts the transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionStartOutcome>> StartAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionStartOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;

        var context = WorkspaceOperationContextFactory.Create(session);
        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return _resultFactory.Conflict<TransactionStartOutcome>(
                WorkspaceErrorCodes.WorkspaceOutOfDate,
                "Reload the workspace before starting a transaction.",
                RequiredAction.ReloadWorkspace,
                context);
        }

        var ownerWorkspaceId = _sessionStore.ReadSnapshot().TransactionOwnerWorkspaceId;
        if (ownerWorkspaceId is not null && ownerWorkspaceId != acquisition.Selection.WorkspaceId)
        {
            var ownerSession = _sessionStore.ReadSession(ownerWorkspaceId.Value);
            return CreateTransactionOwnerResult<TransactionStartOutcome>(ownerSession, context);
        }

        if (session.Transaction is not null)
        {
            return _resultFactory.Rejected<TransactionStartOutcome>(
                WorkspaceErrorCodes.TransactionAlreadyActive,
                "A transaction is already active.",
                RequiredAction.CommitOrRollback,
                context);
        }

        var transaction = new WorkspaceTransaction
        {
            TransactionId = _sessionStore.AllocateWorkspaceTransactionId(),
            BaselineSnapshotId = session.CommittedSnapshotId,
            BaselineSolution = session.CurrentSolution,
            CurrentRevision = 0,
            MaxRevisions = _options.MaxTransactionRevisions,
        };

        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            session.Workspace,
            session.CommittedSnapshotId,
            transaction);

        var updatedSession = session with
        {
            Transaction = transaction,
            CurrentSolution = transaction.CurrentSolution,
            State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionStarted),
            CurrentSnapshotIdentity = snapshotIdentity,
        };

        var admission = _sessionStore.TryStartTransaction(updatedSession);
        if (!admission.IsAdmitted)
        {
            var ownerSession = _sessionStore.ReadSession(admission.ExistingOwnerWorkspaceId.Value);
            return CreateTransactionOwnerResult<TransactionStartOutcome>(ownerSession, context);
        }

        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            transaction.CurrentRevision,
            null,
            null);

        var outcome = new TransactionStartOutcome
        {
            Transaction = transaction.ToInfo(conflicted: false),
        };

        var updatedContext = WorkspaceOperationContextFactory.Create(updatedSession);

        return _resultFactory.Succeeded(outcome, updatedContext);
    }

    /// <summary>
    /// Creates a bounded preview of the active transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="document">The optional document whose detailed diff should be returned.</param>
    /// <param name="includeDiff">Whether the operation result should include a detailed source diff.</param>
    /// <param name="contextLines">The number of unchanged context lines to include around each difference.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionPreviewOutcome>> PreviewAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        DocumentSelector? document,
        bool includeDiff,
        int contextLines,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireShared(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionPreviewOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        if (session?.Transaction is null)
        {
            WorkspaceOperationContext? rejectionContext = null;
            if (session is not null)
            {
                rejectionContext = WorkspaceOperationContextFactory.Create(session);
            }

            return _resultFactory.Rejected<TransactionPreviewOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before previewing changes.",
                RequiredAction.StartTransaction,
                rejectionContext);
        }

        var snapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            session.Transaction.CurrentRevision);
        var resolver = _resolverFactory.Create(
            session.Transaction.CurrentSolution,
            session.Workspace,
            session.ProjectTargetFrameworks,
            snapshot);

        var context = WorkspaceOperationContextFactory.Create(session);
        DocumentReference? diffDocument = null;
        if (includeDiff)
        {
            if (document is null)
            {
                return _resultFactory.Rejected<TransactionPreviewOutcome>(
                    WorkspaceErrorCodes.InvalidRequest,
                    "A document selector is required when includeDiff is true.",
                    context: context);
            }

            var resolution = resolver.ResolveDocument(document);
            if (!resolution.IsResolved)
            {
                var (errorCode, message) = resolution.Status switch
                {
                    SelectorResolveStatus.Ambiguous => (
                        WorkspaceErrorCodes.DocumentAmbiguous,
                        "The document selector matched multiple results."),
                    SelectorResolveStatus.Invalid => (
                        WorkspaceErrorCodes.InvalidRequest,
                        "The document selector contains an invalid path."),
                    _ => (
                        WorkspaceErrorCodes.DocumentNotFound,
                        "The document selector did not match any result."),
                };

                return _resultFactory.Rejected<TransactionPreviewOutcome>(
                    errorCode,
                    message,
                    RequiredAction.ResolveTargetAgain,
                    context);
            }

            diffDocument = resolver.CreateDocumentReference(resolution.Value);
            if (diffDocument is null)
            {
                return _resultFactory.Rejected<TransactionPreviewOutcome>(
                    WorkspaceErrorCodes.DocumentNotFound,
                    "The resolved document cannot be represented within this workspace.",
                    RequiredAction.ResolveTargetAgain,
                    context);
            }
        }

        var changes = await _diffBuilder.CreateChangeSummaryAsync(
            session.Transaction.BaselineSolution,
            session.Transaction.CurrentSolution,
            resolver,
            cancellationToken);

        var documents = changes.Added.Concat(changes.Modified).Concat(changes.Deleted).ToArray();
        DocumentDiff? diff = null;

        if (diffDocument is not null)
        {
            diff = await _diffBuilder.CreateDocumentDiffAsync(
                session.Transaction.BaselineSolution,
                session.Transaction.CurrentSolution,
                diffDocument,
                resolver,
                contextLines,
                cancellationToken);
        }

        var outcome = new TransactionPreviewOutcome
        {
            Transaction = session.Transaction.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            Documents = documents,
            Diff = diff,
        };

        return _resultFactory.Succeeded(outcome, context);
    }

    /// <summary>
    /// Moves the transaction backward or forward through revision history.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="direction">The direction in which to move through transaction history.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionHistoryOutcome>> MoveHistoryAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        TransactionHistoryDirection direction,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionHistoryOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        var transaction = session.Transaction;
        if (transaction is null)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before moving history.",
                RequiredAction.StartTransaction);
        }

        var context = WorkspaceOperationContextFactory.Create(session);
        var snapshotValidation = _snapshotGuard.Validate(session, expectedSnapshot);
        if (!snapshotValidation.IsValid)
        {
            return _resultFactory.Conflict<TransactionHistoryOutcome>(snapshotValidation.Error, context);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return _resultFactory.Conflict<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionConflicted,
                "Roll back the conflicted transaction before changing history.",
                RequiredAction.RollbackTransaction,
                context);
        }

        var updatedTransaction = transaction.MoveHistory(direction);
        if (updatedTransaction is null)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionHistoryUnavailable,
                "The requested transaction history move is unavailable.",
                context: context);
        }

        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            session.Workspace,
            session.CommittedSnapshotId,
            updatedTransaction);

        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
            CurrentSnapshotIdentity = snapshotIdentity,
        };

        _sessionStore.ReplaceSession(updatedSession);
        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            updatedTransaction.CurrentRevision,
            null,
            null);

        var outcome = new TransactionHistoryOutcome
        {
            Transaction = updatedTransaction.ToInfo(conflicted: false),
        };

        var updatedContext = WorkspaceOperationContextFactory.Create(updatedSession);

        return _resultFactory.Succeeded(outcome, updatedContext);
    }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionCommitOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        return await _transactionCommitService.CommitAsync(acquisition.Selection, expectedSnapshot, cancellationToken);
    }

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="alias">The alias used to address the registered item.</param>
    /// <param name="path">The path associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    public async ValueTask<WorkspaceOperationResult<TransactionRollbackOutcome>> RollbackAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            DisposeFailedAcquisition(acquisition);
            return CreateAcquisitionFailureResult<TransactionRollbackOutcome>(acquisition, acquisition.Error);
        }

        using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        var transaction = session.Transaction;
        if (transaction is null)
        {
            return _resultFactory.Rejected<TransactionRollbackOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before rolling back changes.",
                RequiredAction.StartTransaction);
        }

        var rollbackState = TransactionRollbackState.Ready;
        var rollbackTrigger = WorkspaceTrigger.TransactionRolledBack;
        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            rollbackState = TransactionRollbackState.WorkspaceOutOfDate;
            rollbackTrigger = WorkspaceTrigger.ConflictedRollbackCompleted;
        }

        var snapshotIdentity = WorkspaceSnapshotIdentity.Create(
            session.Workspace,
            session.CommittedSnapshotId,
            transaction: null);

        var updatedSession = session with
        {
            Transaction = null,
            CurrentSolution = transaction.BaselineSolution,
            State = _workspaceStateTransitions.Fire(session.State, rollbackTrigger),
            CurrentSnapshotIdentity = snapshotIdentity,
        };

        var completion = _sessionStore.TryCompleteTransaction(updatedSession);
        if (!completion.IsCompleted)
        {
            var context = WorkspaceOperationContextFactory.Create(session);
            return _resultFactory.Faulted<TransactionRollbackOutcome>(
                "TransactionOwnershipChanged",
                completion.Failure.Message,
                context: context);
        }

        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            null,
            null,
            null);

        var outcome = new TransactionRollbackOutcome
        {
            State = rollbackState,
        };

        var updatedContext = WorkspaceOperationContextFactory.Create(updatedSession);

        return _resultFactory.Succeeded(outcome, updatedContext);
    }

    private static string GetWorkspaceDisplayName(WorkspaceSessionSnapshot? session)
    {
        if (session is null)
        {
            return "unknown";
        }

        return session.Workspace.Alias
            ?? session.Workspace.LoadedPath
            ?? session.Workspace.WorkspaceId.ToString();
    }

    private WorkspaceOperationResult<T> CreateTransactionOwnerResult<T>(
        WorkspaceSessionSnapshot? ownerSession,
        WorkspaceOperationContext context)
    {
        return _resultFactory.Rejected<T>(
            WorkspaceErrorCodes.TransactionOwner,
            $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before starting a transaction on this workspace.",
            RequiredAction.CommitOrRollback,
            context);
    }

    private static WorkspaceSelector? CreateWorkspaceSelector(Guid? workspaceId, string? alias, string? path)
    {
        if (workspaceId is null && alias is null && path is null)
        {
            return null;
        }

        return new WorkspaceSelector
        {
            WorkspaceId = workspaceId,
            Alias = alias,
            Path = path,
        };
    }

    private WorkspaceOperationResult<TOutcome> CreateAcquisitionFailureResult<TOutcome>(
        WorkspaceSessionAcquisition acquisition,
        WorkspaceOperationError error)
    {
        WorkspaceOperationContext? context = null;
        if (acquisition.ContextSession is not null)
        {
            context = WorkspaceOperationContextFactory.Create(acquisition.ContextSession);
        }

        return _resultFactory.Rejected<TOutcome>(error, context);
    }

    private static void DisposeFailedAcquisition(WorkspaceSessionAcquisition acquisition)
    {
        acquisition.Lease?.Dispose();
    }
}
