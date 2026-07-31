namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Acquires plugin execution contexts over the neutral Workspace boundary.
/// </summary>
internal interface IToolExecutionContextFactory
{
    /// <summary>Acquires a query context for a plugin request.</summary>
    /// <param name="request">The workspace-bound request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acquired or rejected query lease.</returns>
    ToolExecutionContextLease<IQueryContext> CreateQueryContext(
        WorkspaceBoundRequest request,
        string pluginId,
        string toolName,
        CancellationToken cancellationToken);

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

    /// <summary>Detects a plugin-induced change to the underlying Roslyn Workspace.</summary>
    /// <param name="context">The active plugin execution context.</param>
    /// <returns>A containment failure when the underlying Workspace changed; otherwise <see langword="null"/>.</returns>
    ToolExecutionFailureResult? DetectUnexpectedWorkspaceChange(IToolExecutionContext context);
}
