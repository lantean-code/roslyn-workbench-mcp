using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;
namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class TransactionService : ITransactionService
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSessionAcquirer _sessionAcquirer;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ITransactionCommitService _transactionCommitService;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly IWorkspaceResolverFactory _resolverFactory;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    public TransactionService(
        IOptions<WorkspaceCoordinatorOptions> options,
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

    public async ValueTask<WorkspaceOperationResult<TransactionStartOutcome>> StartAsync(string? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            await DisposeFailedAcquisitionAsync(acquisition).ConfigureAwait(false);
            return CreateAcquisitionFailureResult<TransactionStartOutcome>(acquisition, acquisition.Error);
        }

        await using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;

        var context = CreateContext(session);
        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return _resultFactory.Conflict<TransactionStartOutcome>(
                WorkspaceErrorCodes.WorkspaceOutOfDate,
                "Reload the workspace before starting a transaction.",
                RequiredAction.ReloadWorkspace,
                context);
        }

        var ownerWorkspaceId = _sessionStore.ReadSnapshot().TransactionOwnerWorkspaceId;
        if (!string.IsNullOrWhiteSpace(ownerWorkspaceId) && !string.Equals(ownerWorkspaceId, acquisition.Selection.WorkspaceId, StringComparison.Ordinal))
        {
            var ownerSession = _sessionStore.ReadSession(ownerWorkspaceId);
            return _resultFactory.Rejected<TransactionStartOutcome>(
                WorkspaceErrorCodes.TransactionOwner,
                $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before starting a transaction on this workspace.",
                RequiredAction.CommitOrRollback,
                context);
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
            BaselineSolution = session.CurrentSolution,
            CurrentRevision = 0,
            MaxRevisions = _options.MaxTransactionRevisions,
        };
        var updatedSession = session with
        {
            Transaction = transaction,
            CurrentSolution = transaction.CurrentSolution,
            State = _workspaceStateTransitions.Fire(session.State, WorkspaceTrigger.TransactionStarted),
        };

        _sessionStore.ReplaceSessionAndSetTransactionOwner(updatedSession, acquisition.Selection.WorkspaceId);
        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            transaction.CurrentRevision,
            null,
            null).ConfigureAwait(false);

        return _resultFactory.Succeeded(
            new TransactionStartOutcome
            {
                Transaction = transaction.ToInfo(conflicted: false),
            },
            CreateContext(updatedSession));
    }

    public async ValueTask<WorkspaceOperationResult<TransactionPreviewOutcome>> PreviewAsync(
        string? workspaceId,
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
            await DisposeFailedAcquisitionAsync(acquisition).ConfigureAwait(false);
            return CreateAcquisitionFailureResult<TransactionPreviewOutcome>(acquisition, acquisition.Error);
        }

        await using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        if (session?.Transaction is null)
        {
            return _resultFactory.Rejected<TransactionPreviewOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before previewing changes.",
                RequiredAction.StartTransaction,
                session is null ? null : CreateContext(session));
        }

        var resolver = _resolverFactory.Create(
            session.Transaction.CurrentSolution,
            session.Workspace,
            session.Transaction.CurrentRevision);
        var changes = await _diffBuilder.CreateChangeSummaryAsync(
            session.Transaction.BaselineSolution,
            session.Transaction.CurrentSolution,
            resolver,
            cancellationToken);
        var documents = changes.Added.Concat(changes.Modified).Concat(changes.Deleted).ToArray();
        DocumentDiff? diff = null;

        if (includeDiff && document is not null)
        {
            var resolution = resolver.ResolveDocument(document);
            if (resolution.IsResolved)
            {
                var reference = resolver.CreateDocumentReference(resolution.Value);
                diff = reference is null
                    ? null
                    : await _diffBuilder.CreateDocumentDiffAsync(
                        session.Transaction.BaselineSolution,
                        session.Transaction.CurrentSolution,
                        reference,
                        resolver,
                        contextLines,
                        cancellationToken);
            }
        }

        return _resultFactory.Succeeded(
            new TransactionPreviewOutcome
            {
                Transaction = session.Transaction.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
                Documents = documents,
                Diff = diff,
            },
            CreateContext(session));
    }

    public async ValueTask<WorkspaceOperationResult<TransactionHistoryOutcome>> MoveHistoryAsync(
        string? workspaceId,
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
            await DisposeFailedAcquisitionAsync(acquisition).ConfigureAwait(false);
            return CreateAcquisitionFailureResult<TransactionHistoryOutcome>(acquisition, acquisition.Error);
        }

        await using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        var transaction = session.Transaction;
        if (transaction is null)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before moving history.",
                RequiredAction.StartTransaction);
        }

        var context = CreateContext(session);
        var snapshotMismatch = _snapshotGuard.Validate(session, expectedSnapshot);
        if (snapshotMismatch is not null)
        {
            return _resultFactory.Conflict<TransactionHistoryOutcome>(snapshotMismatch, context);
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

        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
        };

        _sessionStore.ReplaceSession(updatedSession);
        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            updatedTransaction.CurrentRevision,
            null,
            null).ConfigureAwait(false);

        return _resultFactory.Succeeded(
            new TransactionHistoryOutcome
            {
                Transaction = updatedTransaction.ToInfo(conflicted: false),
            },
            CreateContext(updatedSession));
    }

    public async ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        string? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            await DisposeFailedAcquisitionAsync(acquisition).ConfigureAwait(false);
            return CreateAcquisitionFailureResult<TransactionCommitOutcome>(acquisition, acquisition.Error);
        }

        await using var leaseScope = acquisition.Lease;
        return await _transactionCommitService.CommitAsync(acquisition.Selection, expectedSnapshot, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<WorkspaceOperationResult<TransactionRollbackOutcome>> RollbackAsync(string? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireExclusive(CreateWorkspaceSelector(workspaceId, alias, path));
        if (acquisition.HasError)
        {
            await DisposeFailedAcquisitionAsync(acquisition).ConfigureAwait(false);
            return CreateAcquisitionFailureResult<TransactionRollbackOutcome>(acquisition, acquisition.Error);
        }

        await using var leaseScope = acquisition.Lease;
        var session = acquisition.Session;
        var transaction = session.Transaction;
        if (transaction is null)
        {
            return _resultFactory.Rejected<TransactionRollbackOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before rolling back changes.",
                RequiredAction.StartTransaction);
        }

        var rollbackState = session.State == WorkspaceLifecycleState.TransactionConflicted
            ? TransactionRollbackState.WorkspaceOutOfDate
            : TransactionRollbackState.Ready;
        var updatedSession = session with
        {
            Transaction = null,
            CurrentSolution = transaction.BaselineSolution,
            State = _workspaceStateTransitions.Fire(
                session.State,
                session.State == WorkspaceLifecycleState.TransactionConflicted
                    ? WorkspaceTrigger.ConflictedRollbackCompleted
                    : WorkspaceTrigger.TransactionRolledBack),
        };

        _sessionStore.ReplaceSessionAndSetTransactionOwner(updatedSession, null);
        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            null,
            null,
            null).ConfigureAwait(false);

        return _resultFactory.Succeeded(
            new TransactionRollbackOutcome
            {
                State = rollbackState,
            },
            CreateContext(updatedSession));
    }

    private static string GetWorkspaceDisplayName(WorkspaceSessionSnapshot? session)
    {
        if (session is null)
        {
            return "unknown";
        }

        return session.Workspace.Alias
            ?? session.Workspace.LoadedPath
            ?? session.Workspace.WorkspaceId;
    }

    private static Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors.WorkspaceSelector? CreateWorkspaceSelector(string? workspaceId, string? alias, string? path)
    {
        return workspaceId is null && alias is null && path is null
            ? null
            : new Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors.WorkspaceSelector
            {
                WorkspaceId = workspaceId,
                Alias = alias,
                Path = path,
            };
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

    private WorkspaceOperationResult<TOutcome> CreateAcquisitionFailureResult<TOutcome>(
        WorkspaceSessionAcquisition acquisition,
        WorkspaceOperationError error)
    {
        var context = acquisition.ContextSession is null ? null : CreateContext(acquisition.ContextSession);
        return _resultFactory.Rejected<TOutcome>(error, context);
    }

    private static ValueTask DisposeFailedAcquisitionAsync(WorkspaceSessionAcquisition acquisition)
    {
        return acquisition.Lease is null ? ValueTask.CompletedTask : acquisition.Lease.DisposeAsync();
    }
}
