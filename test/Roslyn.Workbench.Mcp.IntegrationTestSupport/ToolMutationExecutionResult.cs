using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class ToolMutationExecutionResult
{
    public PluginExecutionResult<MutationProposal> ProposalResult { get; init; } = new();

    public PluginExecutionResult<MutationData>? StagedResult { get; init; }
}
