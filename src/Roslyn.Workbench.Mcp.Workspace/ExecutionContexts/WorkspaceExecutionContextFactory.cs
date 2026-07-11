using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContextFactory : IWorkspaceExecutionContextFactory
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSelector _workspaceSelector;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly IWorkspaceMutationStager _mutationStager;
    private readonly IWorkspaceResolverFactory _resolverFactory;

    public WorkspaceExecutionContextFactory(
        IOptions<WorkspaceCoordinatorOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSelector workspaceSelector,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        IMutationStagingService mutationStagingService,
        IWorkspaceResolverFactory resolverFactory)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _workspaceSelector = workspaceSelector;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _mutationStager = new WorkspaceMutationStager(mutationStagingService);
        _resolverFactory = resolverFactory;
    }

    public WorkspaceMutationExecutionLease CreateMutationContext(
        WorkspaceSelector? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return WorkspaceMutationExecutionLease.Rejected(CreateWorkspaceRequiredFailure());
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, workspace);
        if (selectionResult.HasError)
        {
            return WorkspaceMutationExecutionLease.Rejected(CreateSelectionFailure(selectionResult.Error));
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return WorkspaceMutationExecutionLease.Rejected(CreateBusyFailure());
        }

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return WorkspaceMutationExecutionLease.Rejected(CreateWorkspaceRequiredFailure(), lease: lease);
        }

        var failure = ValidateMutationSession(selection.WorkspaceId, session, cancellationToken);
        var context = CreateContext(session);
        if (failure is not null)
        {
            return WorkspaceMutationExecutionLease.Rejected(failure, context, _mutationStager, lease);
        }

        return WorkspaceMutationExecutionLease.Acquired(context, _mutationStager, lease);
    }

    public WorkspaceExecutionContextLease CreateQueryContext(
        WorkspaceSelector? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateWorkspaceRequiredFailure());
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, workspace);
        if (selectionResult.HasError)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateSelectionFailure(selectionResult.Error));
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateBusyFailure());
        }

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateWorkspaceRequiredFailure(), lease: lease);
        }

        if (session.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive
            && _workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
        }

        var context = CreateContext(session);
        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateWorkspaceOutOfDateFailure(), context, lease);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateTransactionConflictedFailure(), context, lease);
        }

        return WorkspaceExecutionContextLease.Acquired(context, lease);
    }

    private WorkspaceExecutionContext CreateContext(WorkspaceSessionSnapshot session)
    {
        var resolver = _resolverFactory.Create(
            session.CurrentSolution,
            session.Workspace,
            session.Transaction?.CurrentRevision);
        return new WorkspaceExecutionContext(
            session.CurrentSolution,
            session.Workspace,
            session.Transaction?.CurrentRevision,
            _options.DefaultMaxResults,
            resolver);
    }

    private WorkspaceExecutionFailure? ValidateMutationSession(
        string workspaceId,
        WorkspaceSessionSnapshot session,
        CancellationToken cancellationToken)
    {
        var ownerWorkspaceId = _sessionStore.ReadSnapshot().TransactionOwnerWorkspaceId;
        if (!string.IsNullOrWhiteSpace(ownerWorkspaceId)
            && !string.Equals(ownerWorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            var ownerSession = _sessionStore.ReadSession(ownerWorkspaceId);
            return CreateFailure(
                WorkspaceOperationStatus.Rejected,
                WorkspaceErrorCodes.TransactionOwner,
                $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before mutating this workspace.",
                RequiredAction.CommitOrRollback);
        }

        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return CreateWorkspaceOutOfDateFailure();
        }

        if (_workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return CreateTransactionConflictedFailure();
        }

        if (session.Transaction is null)
        {
            return CreateFailure(
                WorkspaceOperationStatus.Rejected,
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction);
        }

        if (session.Transaction.CurrentRevision >= session.Transaction.MaxRevisions)
        {
            return CreateFailure(
                WorkspaceOperationStatus.Rejected,
                WorkspaceErrorCodes.TransactionCapacity,
                "Reduce transaction history before staging more changes.",
                RequiredAction.ReduceTransactionHistory);
        }

        return null;
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

    private static WorkspaceExecutionFailure CreateSelectionFailure(WorkspaceOperationError error)
    {
        return new WorkspaceExecutionFailure
        {
            Status = WorkspaceOperationStatus.Rejected,
            Error = error,
        };
    }

    private static WorkspaceExecutionFailure CreateBusyFailure()
    {
        return CreateFailure(
            WorkspaceOperationStatus.Rejected,
            WorkspaceErrorCodes.WorkspaceBusy,
            "The workspace is busy.",
            RequiredAction.Retry);
    }

    private static WorkspaceExecutionFailure CreateWorkspaceRequiredFailure()
    {
        return CreateFailure(
            WorkspaceOperationStatus.Rejected,
            WorkspaceErrorCodes.WorkspaceNotOpen,
            "Open a workspace before invoking this tool.",
            RequiredAction.OpenWorkspace);
    }

    private static WorkspaceExecutionFailure CreateWorkspaceOutOfDateFailure()
    {
        return CreateFailure(
            WorkspaceOperationStatus.Conflict,
            WorkspaceErrorCodes.WorkspaceOutOfDate,
            "Reload the workspace before invoking this tool.",
            RequiredAction.ReloadWorkspace);
    }

    private static WorkspaceExecutionFailure CreateTransactionConflictedFailure()
    {
        return CreateFailure(
            WorkspaceOperationStatus.Conflict,
            WorkspaceErrorCodes.TransactionConflicted,
            "Roll back the conflicted transaction before invoking this tool.",
            RequiredAction.RollbackTransaction);
    }

    private static WorkspaceExecutionFailure CreateFailure(
        WorkspaceOperationStatus status,
        string code,
        string message,
        RequiredAction? requiredAction)
    {
        return new WorkspaceExecutionFailure
        {
            Status = status,
            Error = new WorkspaceOperationError
            {
                Code = code,
                Message = message,
                RequiredAction = requiredAction,
            },
        };
    }
}
