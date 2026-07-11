using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;
namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class TransactionService : ITransactionService
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSelector _workspaceSelector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly ISnapshotGuard _snapshotGuard;
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly ITransactionCommitService _transactionCommitService;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly IWorkspaceResolverFactory _resolverFactory;

    public TransactionService(
        IOptions<WorkspaceCoordinatorOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSelector workspaceSelector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        ISnapshotGuard snapshotGuard,
        IWorkspaceOperationResultFactory resultFactory,
        ITransactionCommitService transactionCommitService,
        IWorkspaceDiffBuilder diffBuilder,
        IWorkspaceResolverFactory resolverFactory)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _workspaceSelector = workspaceSelector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _snapshotGuard = snapshotGuard;
        _resultFactory = resultFactory;
        _transactionCommitService = transactionCommitService;
        _diffBuilder = diffBuilder;
        _resolverFactory = resolverFactory;
    }

    public async ValueTask<WorkspaceOperationResult<TransactionStartOutcome>> StartAsync(string? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<TransactionStartOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<TransactionStartOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return _resultFactory.Rejected<TransactionStartOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null || session.CurrentSolution is null)
        {
            return _resultFactory.Rejected<TransactionStartOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

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
        if (!string.IsNullOrWhiteSpace(ownerWorkspaceId) && !string.Equals(ownerWorkspaceId, selection.WorkspaceId, StringComparison.Ordinal))
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

        _sessionStore.ReplaceSessionAndSetTransactionOwner(updatedSession, selection.WorkspaceId);

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

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<TransactionPreviewOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<TransactionPreviewOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return _resultFactory.Rejected<TransactionPreviewOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        var session = _sessionStore.ReadSession(selection.WorkspaceId);
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
            if (resolution.Status == SelectorResolveStatus.Resolved)
            {
                var reference = resolver.CreateDocumentReference(resolution.Value!);
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

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
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

        var nextRevision = direction switch
        {
            TransactionHistoryDirection.Undo when transaction.CurrentRevision > 0 => transaction.CurrentRevision - 1,
            TransactionHistoryDirection.Redo when transaction.CurrentRevision < transaction.Revisions.Count => transaction.CurrentRevision + 1,
            _ => -1,
        };

        if (nextRevision < 0)
        {
            return _resultFactory.Rejected<TransactionHistoryOutcome>(
                WorkspaceErrorCodes.TransactionHistoryUnavailable,
                "The requested transaction history move is unavailable.",
                context: context);
        }

        var updatedTransaction = transaction with
        {
            CurrentRevision = nextRevision,
        };
        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
        };

        _sessionStore.ReplaceSession(updatedSession);

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

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return _resultFactory.Rejected<TransactionCommitOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        return await _transactionCommitService.CommitAsync(selection, expectedSnapshot, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<WorkspaceOperationResult<TransactionRollbackOutcome>> RollbackAsync(string? workspaceId, string? alias, string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return _resultFactory.Rejected<TransactionRollbackOutcome>(
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, CreateWorkspaceSelector(workspaceId, alias, path));
        if (selectionResult.HasError)
        {
            return _resultFactory.Rejected<TransactionRollbackOutcome>(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return _resultFactory.Rejected<TransactionRollbackOutcome>(
                WorkspaceErrorCodes.WorkspaceBusy,
                "The workspace is busy.",
                RequiredAction.Retry,
                CreateContext(selection.Session));
        }

        await using var leaseScope = lease;
        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        var transaction = session?.Transaction;
        if (session is null || transaction is null)
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
}
