using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContextFactory : IWorkspaceExecutionContextFactory
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSessionAcquirer _sessionAcquirer;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly IWorkspaceMutationStager _mutationStager;
    private readonly IWorkspaceResolverFactory _resolverFactory;

    public WorkspaceExecutionContextFactory(
        IOptions<WorkspaceCoordinatorOptions> options,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSessionAcquirer sessionAcquirer,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        IMutationStagingService mutationStagingService,
        IWorkspaceResolverFactory resolverFactory)
    {
        _options = options.Value;
        _sessionStore = sessionStore;
        _sessionAcquirer = sessionAcquirer;
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

        var acquisition = _sessionAcquirer.AcquireExclusive(workspace);
        if (acquisition.HasError)
        {
            var failureContext = acquisition.ContextSession is null ? null : CreateContext(acquisition.ContextSession);
            return WorkspaceMutationExecutionLease.Rejected(
                CreateSelectionFailure(acquisition.Error),
                failureContext,
                failureContext is null ? null : _mutationStager,
                acquisition.Lease);
        }

        var failure = ValidateMutationSession(acquisition.Selection.WorkspaceId, acquisition.Session, cancellationToken);
        var context = CreateContext(acquisition.Session);
        if (failure is not null)
        {
            return WorkspaceMutationExecutionLease.Rejected(failure, context, _mutationStager, acquisition.Lease);
        }

        return WorkspaceMutationExecutionLease.Acquired(context, _mutationStager, acquisition.Lease);
    }

    public WorkspaceExecutionContextLease CreateQueryContext(
        WorkspaceSelector? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquisition = _sessionAcquirer.AcquireShared(workspace);
        if (acquisition.HasError)
        {
            var failureContext = acquisition.ContextSession is null ? null : CreateContext(acquisition.ContextSession);
            return WorkspaceExecutionContextLease.Rejected(
                CreateSelectionFailure(acquisition.Error),
                failureContext,
                acquisition.Lease);
        }

        var session = acquisition.Session;
        if (session.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive
            && _workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
        }

        var context = CreateContext(session);
        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateWorkspaceOutOfDateFailure(), context, acquisition.Lease);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return WorkspaceExecutionContextLease.Rejected(CreateTransactionConflictedFailure(), context, acquisition.Lease);
        }

        return WorkspaceExecutionContextLease.Acquired(context, acquisition.Lease);
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
