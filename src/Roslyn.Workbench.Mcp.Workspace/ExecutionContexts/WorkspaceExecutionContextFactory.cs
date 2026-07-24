using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContextFactory : IWorkspaceExecutionContextFactory
{
    private readonly WorkspaceOptions _options;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSessionAcquirer _sessionAcquirer;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly IWorkspaceMutationStager _mutationStager;
    private readonly IWorkspaceResolverFactory _resolverFactory;

    public WorkspaceExecutionContextFactory(
        IOptions<WorkspaceOptions> options,
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

        WorkspaceSessionAcquisition acquisition;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            "mutation-context",
            WorkbenchPerformanceEventSource.WorkspaceLeaseAcquisitionPhase))
        {
            acquisition = _sessionAcquirer.AcquireExclusive(workspace);
        }

        if (acquisition.HasError)
        {
            var failureContext = acquisition.ContextSession is null ? null : CreateContext(acquisition.ContextSession);
            return WorkspaceMutationExecutionLease.Rejected(
                CreateSelectionFailure(acquisition.Error),
                failureContext,
                failureContext is null ? null : _mutationStager,
                acquisition.Lease);
        }

        try
        {
            var validation = ValidateMutationSession(
                acquisition.Selection.WorkspaceId,
                acquisition.Session,
                cancellationToken);

            WorkspaceExecutionContext context;
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                "mutation-context",
                WorkbenchPerformanceEventSource.ContextConstructionPhase))
            {
                context = CreateContext(validation.Session);
            }

            if (validation.Failure is not null)
            {
                return WorkspaceMutationExecutionLease.Rejected(
                    validation.Failure,
                    context,
                    _mutationStager,
                    acquisition.Lease);
            }

            return WorkspaceMutationExecutionLease.Acquired(context, _mutationStager, acquisition.Lease);
        }
        catch
        {
            acquisition.Lease.Dispose();
            throw;
        }
    }

    public WorkspaceExecutionContextLease CreateQueryContext(
        WorkspaceSelector? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WorkspaceSessionAcquisition acquisition;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            "query-context",
            WorkbenchPerformanceEventSource.WorkspaceLeaseAcquisitionPhase))
        {
            acquisition = _sessionAcquirer.AcquireShared(workspace);
        }

        if (acquisition.HasError)
        {
            var failureContext = acquisition.ContextSession is null ? null : CreateContext(acquisition.ContextSession);
            return WorkspaceExecutionContextLease.Rejected(
                CreateSelectionFailure(acquisition.Error),
                failureContext,
                acquisition.Lease);
        }

        try
        {
            var validation = ValidateQuerySession(acquisition.Session, cancellationToken);

            WorkspaceExecutionContext context;
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                "query-context",
                WorkbenchPerformanceEventSource.ContextConstructionPhase))
            {
                context = CreateContext(validation.Session);
            }

            if (validation.Failure is not null)
            {
                return WorkspaceExecutionContextLease.Rejected(validation.Failure, context, acquisition.Lease);
            }

            return WorkspaceExecutionContextLease.Acquired(context, acquisition.Lease);
        }
        catch
        {
            acquisition.Lease.Dispose();
            throw;
        }
    }

    public WorkspaceExecutionFailure? DetectUnexpectedWorkspaceChange(string workspaceId)
    {
        var session = _sessionStore.ReadSession(workspaceId);
        if (session is null)
        {
            return CreateFailure(
                WorkspaceOperationStatus.Rejected,
                WorkspaceErrorCodes.WorkspaceNotOpen,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }

        var effectiveSession = RefreshUnexpectedWorkspaceChange(session);
        return CreateUnavailableStateFailure(effectiveSession.State);
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

    private WorkspaceExecutionSessionValidation ValidateQuerySession(
        WorkspaceSessionSnapshot session,
        CancellationToken cancellationToken)
    {
        var effectiveSession = RefreshExternalChanges(session, cancellationToken);
        var failure = CreateUnavailableStateFailure(effectiveSession.State);

        return new WorkspaceExecutionSessionValidation(effectiveSession, failure);
    }

    private WorkspaceExecutionSessionValidation ValidateMutationSession(
        string workspaceId,
        WorkspaceSessionSnapshot session,
        CancellationToken cancellationToken)
    {
        var snapshot = _sessionStore.ReadSnapshot();
        var ownerWorkspaceId = snapshot.TransactionOwnerWorkspaceId;
        if (!string.IsNullOrWhiteSpace(ownerWorkspaceId)
            && !string.Equals(ownerWorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            var ownerSession = _sessionStore.ReadSession(ownerWorkspaceId);
            var failure = CreateFailure(
                WorkspaceOperationStatus.Rejected,
                WorkspaceErrorCodes.TransactionOwner,
                $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before mutating this workspace.",
                RequiredAction.CommitOrRollback);

            return new WorkspaceExecutionSessionValidation(session, failure);
        }

        var effectiveSession = RefreshExternalChanges(session, cancellationToken);
        var unavailableStateFailure = CreateUnavailableStateFailure(effectiveSession.State);
        if (unavailableStateFailure is not null)
        {
            return new WorkspaceExecutionSessionValidation(effectiveSession, unavailableStateFailure);
        }

        if (effectiveSession.Transaction is null)
        {
            var failure = CreateFailure(
                WorkspaceOperationStatus.Rejected,
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction);

            return new WorkspaceExecutionSessionValidation(effectiveSession, failure);
        }

        if (effectiveSession.Transaction.CurrentRevision >= effectiveSession.Transaction.MaxRevisions)
        {
            var failure = CreateFailure(
                WorkspaceOperationStatus.Rejected,
                WorkspaceErrorCodes.TransactionCapacity,
                "Reduce transaction history before staging more changes.",
                RequiredAction.ReduceTransactionHistory);

            return new WorkspaceExecutionSessionValidation(effectiveSession, failure);
        }

        return new WorkspaceExecutionSessionValidation(effectiveSession);
    }

    private WorkspaceSessionSnapshot RefreshExternalChanges(
        WorkspaceSessionSnapshot session,
        CancellationToken cancellationToken)
    {
        if (session.State is not (WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive))
        {
            return session;
        }

        if (session.LoadedWorkspace.HasCurrentSolutionChanged)
        {
            return TransitionForUnexpectedWorkspaceChange(session);
        }

        if (!_workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            return session;
        }

        return TransitionForUnexpectedWorkspaceChange(session);
    }

    private WorkspaceSessionSnapshot RefreshUnexpectedWorkspaceChange(WorkspaceSessionSnapshot session)
    {
        if (session.State is not (WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive)
            || !session.LoadedWorkspace.HasCurrentSolutionChanged)
        {
            return session;
        }

        return TransitionForUnexpectedWorkspaceChange(session);
    }

    private WorkspaceSessionSnapshot TransitionForUnexpectedWorkspaceChange(WorkspaceSessionSnapshot session)
    {
        var transitionedSession = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
        _sessionStore.ReplaceSession(transitionedSession);
        return transitionedSession;
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

    private static WorkspaceExecutionFailure? CreateUnavailableStateFailure(WorkspaceLifecycleState state)
    {
        return state switch
        {
            WorkspaceLifecycleState.WorkspaceOutOfDate => CreateWorkspaceOutOfDateFailure(),
            WorkspaceLifecycleState.TransactionConflicted => CreateTransactionConflictedFailure(),
            _ => null,
        };
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
