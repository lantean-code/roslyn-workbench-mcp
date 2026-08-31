namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Adapts an <see cref="MSBuildWorkspace"/> to the host's owned workspace lifetime.
/// </summary>
internal sealed class LoadedWorkspace : ILoadedWorkspace
{
    private readonly MSBuildWorkspace _workspace;
    private readonly Solution _initialSolution;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadedWorkspace"/> class.
    /// </summary>
    /// <param name="workspace">The MSBuild workspace owned by this instance.</param>
    public LoadedWorkspace(MSBuildWorkspace workspace)
    {
        _workspace = workspace;
        _initialSolution = workspace.CurrentSolution;
    }

    /// <inheritdoc/>
    public Solution CurrentSolution => _workspace.CurrentSolution;

    /// <inheritdoc/>
    public bool HasCurrentSolutionChanged => !ReferenceEquals(_initialSolution, _workspace.CurrentSolution);

    /// <inheritdoc/>
    public void Dispose()
    {
        _workspace.Dispose();
    }
}
