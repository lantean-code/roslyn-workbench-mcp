namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Acquires plugin execution contexts over the neutral Workspace boundary.
/// </summary>
public interface IToolExecutionContextFactory
{
    /// <summary>Acquires a query context for a plugin request.</summary>
    /// <param name="request">The workspace-bound request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acquired or rejected query lease.</returns>
    ToolExecutionContextLease<IQueryContext> CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken);

    /// <summary>Acquires a mutation context and its Host-only staging capability.</summary>
    /// <param name="request">The workspace-bound request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acquired or rejected mutation lease.</returns>
    PluginMutationExecutionLease CreateMutationContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken);
}
