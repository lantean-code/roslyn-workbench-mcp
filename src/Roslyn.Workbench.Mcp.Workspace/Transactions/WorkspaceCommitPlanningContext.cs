namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Accumulates validated paths, file operations, and recovery artifacts while building a commit plan.
/// </summary>
internal sealed class WorkspaceCommitPlanningContext
{
    /// <summary>
    /// Gets the durable commit identifier.
    /// </summary>
    public string CommitId { get; }

    /// <summary>
    /// Gets the solution or project path loaded by the workspace.
    /// </summary>
    public string LoadedPath { get; }

    /// <summary>
    /// Gets the root that bounds every planned file operation.
    /// </summary>
    public string WorkspaceRoot { get; }

    /// <summary>
    /// Gets the project roots allowed to contain changed documents.
    /// </summary>
    public IReadOnlyList<FileSystemPathKey> ProjectRoots { get; }

    /// <summary>
    /// Gets physical document paths present in the transaction baseline.
    /// </summary>
    public HashSet<FileSystemPathKey> BaselineDocumentPaths { get; }

    /// <summary>
    /// Gets planned entries indexed by target path for conflict detection.
    /// </summary>
    public Dictionary<FileSystemPathKey, WorkspaceCommitEntry> EntriesByTarget { get; } = [];

    /// <summary>
    /// Gets planned file operations in application order.
    /// </summary>
    public List<WorkspaceCommitEntry> Entries { get; } = [];

    /// <summary>
    /// Gets recovery artifacts indexed by their manifest path.
    /// </summary>
    public Dictionary<string, ReadOnlyMemory<byte>> Artifacts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets directories that must be created while applying the commit.
    /// </summary>
    public HashSet<FileSystemPathKey> CreatedDirectories { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceCommitPlanningContext"/> class.
    /// </summary>
    /// <param name="commitId">The commit identifier.</param>
    /// <param name="loadedPath">The path loaded into the workspace.</param>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="projectRoots">The canonical project roots used to validate planned file operations.</param>
    /// <param name="baselineDocumentPaths">The document paths present in the transaction baseline.</param>
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
