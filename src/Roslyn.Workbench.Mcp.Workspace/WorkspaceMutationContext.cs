using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class WorkspaceMutationContext : IMutationContext
{
    private readonly Func<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken, ValueTask<PluginExecutionResult<MutationData>>> _stageAsync;

    public WorkspaceMutationContext(
        Solution currentSolution,
        WorkspaceIdentity? workspaceIdentity,
        int? transactionRevision,
        ResultLimit effectiveResultLimit,
        IWorkspaceResolver resolver,
        ICodeActionService codeActionService,
        Func<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken, ValueTask<PluginExecutionResult<MutationData>>> stageAsync,
        IToolExecutionServices toolExecutionServices)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        TransactionRevision = transactionRevision;
        EffectiveResultLimit = effectiveResultLimit;
        WorkspaceResolver = resolver;
        CodeActionService = codeActionService;
        _stageAsync = stageAsync;
        ToolExecutionServices = toolExecutionServices ?? throw new ArgumentNullException(nameof(toolExecutionServices));
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity? WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public ResultLimit EffectiveResultLimit { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }

    public ICodeActionService CodeActionService { get; }

    public IToolExecutionServices ToolExecutionServices { get; }

    public ValueTask<PluginExecutionResult<MutationData>> StageAsync(
        RegisteredTool tool,
        MutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        return _stageAsync(tool, proposal, diagnostics, warnings, cancellationToken);
    }

}
