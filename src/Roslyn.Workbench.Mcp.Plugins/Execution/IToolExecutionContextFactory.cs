using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Creates the host-owned execution contexts used by the tool executor.
/// </summary>
public interface IToolExecutionContextFactory
{
    /// <summary>
    /// Creates the query context for a tool invocation.
    /// </summary>
    /// <param name="request">The deserialized workspace-bound request payload.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The host-owned query context lease or a short-circuit result.</returns>
    ToolExecutionContextLease<IQueryContext> CreateQueryContext(WorkspaceBoundRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the mutation context for a tool invocation.
    /// </summary>
    /// <param name="request">The deserialized workspace-bound request payload.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The host-owned mutation context lease or a short-circuit result.</returns>
    ToolExecutionContextLease<IMutationContext> CreateMutationContext(WorkspaceBoundRequest request, CancellationToken cancellationToken);
}
