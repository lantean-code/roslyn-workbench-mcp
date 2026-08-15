namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitPlanningContext
{
    public string CommitId { get; }

    public string LoadedPath { get; }

    public string WorkspaceRoot { get; }

    public IReadOnlyList<FileSystemPathKey> ProjectRoots { get; }

    public HashSet<FileSystemPathKey> BaselineDocumentPaths { get; }

    public Dictionary<FileSystemPathKey, WorkspaceCommitEntry> EntriesByTarget { get; } = [];

    public List<WorkspaceCommitEntry> Entries { get; } = [];

    public Dictionary<string, ReadOnlyMemory<byte>> Artifacts { get; } = new(StringComparer.Ordinal);

    public HashSet<FileSystemPathKey> CreatedDirectories { get; } = [];

    public WorkspaceCommitPlanningContext(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        IReadOnlyList<FileSystemPathKey> projectRoots,
        HashSet<FileSystemPathKey> baselineDocumentPaths)
    {
        CommitId = commitId;
        LoadedPath = loadedPath;
        WorkspaceRoot = workspaceRoot;
        ProjectRoots = projectRoots;
        BaselineDocumentPaths = baselineDocumentPaths;
    }
}
