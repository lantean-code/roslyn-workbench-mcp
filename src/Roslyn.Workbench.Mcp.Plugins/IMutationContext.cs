namespace Roslyn.Workbench.Mcp.Plugins;

using Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents the host-owned execution context for a mutation tool.
/// </summary>
public interface IMutationContext :
    IToolExecutionContext
{
    /// <summary>
    /// Stages a plugin-produced mutation proposal into the active transaction.
    /// </summary>
    /// <param name="tool">The mutation tool being executed.</param>
    /// <param name="proposal">The candidate mutation proposal.</param>
    /// <param name="diagnostics">The diagnostics produced before staging.</param>
    /// <param name="warnings">The warnings produced before staging.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized staged mutation result.</returns>
    ValueTask<PluginExecutionResult<MutationData>> StageAsync(
        RegisteredTool tool,
        MutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken);
}
