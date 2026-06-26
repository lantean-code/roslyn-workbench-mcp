namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Creates the host-owned execution contexts used by the tool executor.
/// </summary>
public interface IToolExecutionContextFactory
{
    /// <summary>
    /// Creates the query context for a tool invocation.
    /// </summary>
    /// <param name="tool">The tool being executed.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The host-owned query context lease or a short-circuit result.</returns>
    ValueTask<ToolExecutionContextLease<IQueryContext>> CreateQueryContextAsync(RegisteredTool tool, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the mutation context for a tool invocation.
    /// </summary>
    /// <param name="tool">The tool being executed.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The host-owned mutation context lease or a short-circuit result.</returns>
    ValueTask<ToolExecutionContextLease<IMutationContext>> CreateMutationContextAsync(RegisteredTool tool, CancellationToken cancellationToken);
}
