namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Owns a loaded Roslyn workspace and exposes its current solution state.
/// </summary>
internal interface ILoadedWorkspace : IDisposable
{
    /// <summary>
    /// Gets the workspace's current solution.
    /// </summary>
    Solution CurrentSolution { get; }

    /// <summary>
    /// Gets a value indicating whether the workspace's current solution differs from the solution captured when loading completed.
    /// </summary>
    bool HasCurrentSolutionChanged { get; }
}
