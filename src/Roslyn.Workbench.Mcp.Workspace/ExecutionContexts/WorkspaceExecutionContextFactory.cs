using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContextFactory : IWorkspaceExecutionContextFactory
{
    private readonly WorkspaceCoordinatorOptions _options;
    private readonly ICodeActionQueryWorkflow _codeActionQueryWorkflow;
    private readonly ICodeActionMutationWorkflow _codeActionMutationWorkflow;
    private readonly IToolExecutionServices _toolExecutionServices;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSelector _workspaceSelector;
    private readonly IWorkspaceChangeDetector _workspaceChangeDetector;
    private readonly IWorkspaceStateTransitions _workspaceStateTransitions;
    private readonly IMutationStagingService _mutationStagingService;

    public WorkspaceExecutionContextFactory(
        IOptions<WorkspaceCoordinatorOptions> options,
        ICodeActionQueryWorkflow codeActionQueryWorkflow,
        ICodeActionMutationWorkflow codeActionMutationWorkflow,
        IToolExecutionServices toolExecutionServices,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceSelector workspaceSelector,
        IWorkspaceChangeDetector workspaceChangeDetector,
        IWorkspaceStateTransitions workspaceStateTransitions,
        IMutationStagingService mutationStagingService)
    {
        _options = options.Value;
        _codeActionQueryWorkflow = codeActionQueryWorkflow;
        _codeActionMutationWorkflow = codeActionMutationWorkflow;
        _toolExecutionServices = toolExecutionServices;
        _sessionStore = sessionStore;
        _workspaceSelector = workspaceSelector;
        _workspaceChangeDetector = workspaceChangeDetector;
        _workspaceStateTransitions = workspaceStateTransitions;
        _mutationStagingService = mutationStagingService;
    }

    public ToolExecutionContextLease<IMutationContext> CreateMutationContext(WorkspaceBoundRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return ToolExecutionContextLease<IMutationContext>.Rejected(CreateWorkspaceRequiredResult());
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, request.Workspace);
        if (selectionResult.HasError)
        {
            return ToolExecutionContextLease<IMutationContext>.Rejected(CreatePluginResult(selectionResult.Error));
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireExclusive();
        if (lease is null)
        {
            return ToolExecutionContextLease<IMutationContext>.Rejected(CreateBusyResult(selection.Session));
        }

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return ToolExecutionContextLease<IMutationContext>.Rejected(CreateWorkspaceRequiredResult(), lease: lease);
        }

        var rejection = ValidateMutationSession(selection.WorkspaceId, session, cancellationToken);
        if (rejection is not null)
        {
            return ToolExecutionContextLease<IMutationContext>.Rejected(rejection, CreateMutationContext(session), lease);
        }

        return ToolExecutionContextLease<IMutationContext>.Acquired(CreateMutationContext(session), lease);
    }

    public ToolExecutionContextLease<IQueryContext> CreateQueryContext(WorkspaceBoundRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return ToolExecutionContextLease<IQueryContext>.Rejected(CreateWorkspaceRequiredResult());
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, request.Workspace);
        if (selectionResult.HasError)
        {
            return ToolExecutionContextLease<IQueryContext>.Rejected(CreatePluginResult(selectionResult.Error));
        }

        var selection = selectionResult.Selection;
        var lease = selection.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return ToolExecutionContextLease<IQueryContext>.Rejected(CreateBusyResult(selection.Session));
        }

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return ToolExecutionContextLease<IQueryContext>.Rejected(CreateWorkspaceRequiredResult(), lease: lease);
        }

        if (session.State is WorkspaceLifecycleState.Ready or WorkspaceLifecycleState.TransactionActive
            && _workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
        }

        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return ToolExecutionContextLease<IQueryContext>.Rejected(CreateWorkspaceOutOfDateResult(), CreateQueryContext(session), lease);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return ToolExecutionContextLease<IQueryContext>.Rejected(CreateTransactionConflictedResult(), CreateQueryContext(session), lease);
        }

        return ToolExecutionContextLease<IQueryContext>.Acquired(CreateQueryContext(session), lease);
    }

    private WorkspaceQueryContext CreateQueryContext(WorkspaceSessionSnapshot session)
    {
        var resolver = new WorkspaceResolver(session.CurrentSolution, session.Workspace, session.Transaction?.CurrentRevision);
        return new WorkspaceQueryContext(
            session.CurrentSolution,
            session.Workspace,
            session.Transaction?.CurrentRevision,
            _options.DefaultMaxResults,
            resolver,
            _codeActionQueryWorkflow,
            _toolExecutionServices);
    }

    private WorkspaceMutationContext CreateMutationContext(WorkspaceSessionSnapshot session)
    {
        var resolver = new WorkspaceResolver(session.CurrentSolution, session.Workspace, session.Transaction?.CurrentRevision);
        return new WorkspaceMutationContext(
            session.CurrentSolution,
            session.Workspace,
            session.Transaction?.CurrentRevision,
            _options.DefaultMaxResults,
            resolver,
            _codeActionMutationWorkflow,
            StageMutationAsync,
            _toolExecutionServices);
    }

    private ToolExecutionFailureResult? ValidateMutationSession(string workspaceId, WorkspaceSessionSnapshot session, CancellationToken cancellationToken)
    {
        var ownerWorkspaceId = _sessionStore.ReadSnapshot().TransactionOwnerWorkspaceId;
        if (!string.IsNullOrWhiteSpace(ownerWorkspaceId) && !string.Equals(ownerWorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            var ownerSession = _sessionStore.ReadSession(ownerWorkspaceId);
            return new ToolExecutionFailureResult
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateToolError(
                    WorkspaceErrorCodes.TransactionOwner,
                    $"Commit or roll back the transaction on workspace '{GetWorkspaceDisplayName(ownerSession)}' before mutating this workspace."),
                RequiredAction = RequiredAction.CommitOrRollback,
            };
        }

        if (session.State == WorkspaceLifecycleState.WorkspaceOutOfDate)
        {
            return CreateWorkspaceOutOfDateResult();
        }

        if (_workspaceChangeDetector.HasChanged(session.InputManifest, cancellationToken))
        {
            session = _workspaceStateTransitions.ApplyExternalChangeDetected(session);
            _sessionStore.ReplaceSession(session);
        }

        if (session.State == WorkspaceLifecycleState.TransactionConflicted)
        {
            return CreateTransactionConflictedResult();
        }

        if (session.Transaction is null)
        {
            return CreateNoActiveTransactionResult();
        }

        if (session.Transaction.CurrentRevision >= session.Transaction.MaxRevisions)
        {
            return new ToolExecutionFailureResult
            {
                Outcome = ToolOutcome.Rejected,
                Error = CreateToolError(WorkspaceErrorCodes.TransactionCapacity, "Reduce transaction history before staging more changes."),
                RequiredAction = RequiredAction.ReduceTransactionHistory,
            };
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

    private static ToolExecutionFailureResult CreatePluginResult(WorkspaceOperationError error)
    {
        return new ToolExecutionFailureResult
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateToolError(error.Code, error.Message),
            RequiredAction = error.RequiredAction,
        };
    }

    private async ValueTask<PluginExecutionResult<MutationData>> StageMutationAsync(
        RegisteredTool tool,
        MutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        var result = await _mutationStagingService
            .StageAsync(tool.Metadata.Name, proposal, diagnostics, warnings, cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            WorkspaceOperationStatus.Succeeded => PluginExecutionResult<MutationData>.Success(
                new MutationData
                {
                    Operation = result.Data!.Operation,
                    Summary = result.Data.Summary,
                    Transaction = result.Data.Transaction,
                    Preview = result.Data.Preview,
                },
                result.Data.Changes,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Rejected => PluginExecutionResult<MutationData>.Rejected(
                CreateToolError(result.Error!.Code, result.Error.Message),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Conflict => PluginExecutionResult<MutationData>.Conflict(
                CreateToolError(result.Error!.Code, result.Error.Message),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Faulted => new PluginExecutionResult<MutationData>
            {
                Outcome = ToolOutcome.Faulted,
                Error = CreateToolError(result.Error!.Code, result.Error.Message),
                RequiredAction = result.Error.RequiredAction,
                Diagnostics = result.Diagnostics,
                Warnings = result.Warnings,
            },
            WorkspaceOperationStatus.NoChange => PluginExecutionResult<MutationData>.NoChange(
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => throw new InvalidOperationException($"Unsupported workspace operation status '{result.Status}'."),
        };
    }

    private static ToolExecutionFailureResult CreateBusyResult(WorkspaceSessionSnapshot? session = null)
    {
        _ = session;
        return new ToolExecutionFailureResult
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateToolError(WorkspaceErrorCodes.WorkspaceBusy, "The workspace is busy."),
            RequiredAction = RequiredAction.Retry,
        };
    }

    private static ToolExecutionFailureResult CreateNoActiveTransactionResult()
    {
        return new ToolExecutionFailureResult
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateToolError(WorkspaceErrorCodes.TransactionRequired, "Start a transaction before invoking mutation tools."),
            RequiredAction = RequiredAction.StartTransaction,
        };
    }

    private static ToolExecutionFailureResult CreateWorkspaceRequiredResult()
    {
        return new ToolExecutionFailureResult
        {
            Outcome = ToolOutcome.Rejected,
            Error = CreateToolError(WorkspaceErrorCodes.WorkspaceNotOpen, "Open a workspace before invoking this tool."),
            RequiredAction = RequiredAction.OpenWorkspace,
        };
    }

    private static ToolExecutionFailureResult CreateWorkspaceOutOfDateResult()
    {
        return new ToolExecutionFailureResult
        {
            Outcome = ToolOutcome.Conflict,
            Error = CreateToolError(WorkspaceErrorCodes.WorkspaceOutOfDate, "Reload the workspace before invoking this tool."),
            RequiredAction = RequiredAction.ReloadWorkspace,
        };
    }

    private static ToolExecutionFailureResult CreateTransactionConflictedResult()
    {
        return new ToolExecutionFailureResult
        {
            Outcome = ToolOutcome.Conflict,
            Error = CreateToolError(WorkspaceErrorCodes.TransactionConflicted, "Roll back the conflicted transaction before invoking this tool."),
            RequiredAction = RequiredAction.RollbackTransaction,
        };
    }

    private static ToolError CreateToolError(string code, string message)
    {
        return new ToolError
        {
            Code = code,
            Message = message,
        };
    }
}
