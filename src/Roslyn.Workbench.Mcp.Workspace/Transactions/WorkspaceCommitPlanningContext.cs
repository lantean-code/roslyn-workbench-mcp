namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitPlanningContext
{
    public string CommitId { get; }

    public string LoadedPath { get; }

    public string WorkspaceRoot { get; }

    public IReadOnlyList<string> ProjectRoots { get; }

    public HashSet<string> BaselineDocumentPaths { get; }

    public HashSet<string> Targets { get; }

    public List<WorkspaceCommitEntry> Entries { get; } = [];

    public Dictionary<string, ReadOnlyMemory<byte>> Artifacts { get; } = new(StringComparer.Ordinal);

    public HashSet<string> CreatedDirectories { get; }

    public WorkspaceCommitPlanningContext(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        IReadOnlyList<string> projectRoots,
        HashSet<string> baselineDocumentPaths,
        IEqualityComparer<string> pathComparer)
    {
        CommitId = commitId;
        LoadedPath = loadedPath;
        WorkspaceRoot = workspaceRoot;
        ProjectRoots = projectRoots;
        BaselineDocumentPaths = baselineDocumentPaths;
        Targets = new HashSet<string>(pathComparer);
        CreatedDirectories = new HashSet<string>(pathComparer);
    }
}
