namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal sealed record ComponentWorkspaceOptions
{
    public ComponentWorkspaceBoundary Boundary { get; init; } = ComponentWorkspaceBoundary.Workspace;

    public int MaxConcurrentQueries { get; init; } = 2;

    public int DefaultMaxResults { get; init; } = 100;

    public int MaxTransactionRevisions { get; init; } = 20;

    public int MaxLoadedWorkspaces { get; init; } = 4;

    public bool IncludeBuiltInCodeActions { get; init; }

    public IWorkspaceCommitPlanner? CommitPlanner { get; init; }

    public string? StateDirectory { get; init; }
}

internal enum ComponentWorkspaceBoundary
{
    Workspace,
    Plugins,
    CodeActions,
}
