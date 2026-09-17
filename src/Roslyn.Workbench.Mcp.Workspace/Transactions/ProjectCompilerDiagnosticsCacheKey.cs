using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies cached compiler diagnostics for one project within a solution-scoped cache.
/// </summary>
internal sealed record ProjectCompilerDiagnosticsCacheKey : IWorkspaceQueryCacheKey
{
    /// <summary>
    /// Gets the Roslyn project identifier represented by the key.
    /// </summary>
    public Guid ProjectId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectCompilerDiagnosticsCacheKey"/> class.
    /// </summary>
    /// <param name="projectId">The Roslyn project identifier represented by the key.</param>
    public ProjectCompilerDiagnosticsCacheKey(Guid projectId)
    {
        ProjectId = projectId;
    }
}
