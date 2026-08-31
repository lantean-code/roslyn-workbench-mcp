namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Acquires a physically contained cross-process commit lock for a workspace root.
/// </summary>
internal sealed class WorkspaceCommitLockManager : IWorkspaceCommitLockManager
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceFileLockProvider _fileLockProvider;
    private readonly IPhysicalPathContainment _pathContainment;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceCommitLockManager"/> class.
    /// </summary>
    /// <param name="fileSystem">The file-system abstraction used for storage operations.</param>
    /// <param name="fileLockProvider">The provider used to acquire the lock file.</param>
    /// <param name="pathContainment">The service used to test whether paths belong to the workspace.</param>
    public WorkspaceCommitLockManager(
        IFileSystem fileSystem,
        IWorkspaceFileLockProvider fileLockProvider,
        IPhysicalPathContainment pathContainment)
    {
        _fileSystem = fileSystem;
        _fileLockProvider = fileLockProvider;
        _pathContainment = pathContainment;
    }

    /// <summary>
    /// Acquires the workspace commit lock.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <returns>The acquired lock or a classified contention or failure result.</returns>
    public WorkspaceCommitLockAcquisition Acquire(string workspaceRoot)
    {
        try
        {
            var canonicalRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
            var directory = _fileSystem.Path.Combine(canonicalRoot, ".vs", "roslyn-workbench-mcp", "locks");
            var lockPath = _fileSystem.Path.Combine(directory, "commit.lock");
            if (!_pathContainment.TryGetStrictlyContainedPath(
                canonicalRoot,
                lockPath,
                out var containedLockPath))
            {
                return WorkspaceCommitLockAcquisition.Failed(
                    "The Workspace commit lock path resolves outside the workspace root.");
            }

            var containedDirectory = _fileSystem.Path.GetDirectoryName(containedLockPath);
            if (containedDirectory is null)
            {
                return WorkspaceCommitLockAcquisition.Failed(
                    "The Workspace commit lock path does not have a parent directory.");
            }

            _fileSystem.Directory.CreateDirectory(containedDirectory);
            if (!_pathContainment.TryGetStrictlyContainedPath(
                canonicalRoot,
                containedLockPath,
                out var revalidatedLockPath))
            {
                return WorkspaceCommitLockAcquisition.Failed(
                    "The Workspace commit lock path resolves outside the workspace root.");
            }

            var ownership = _fileLockProvider.TryAcquire(revalidatedLockPath);
            if (ownership is null)
            {
                return WorkspaceCommitLockAcquisition.Contended();
            }

            return WorkspaceCommitLockAcquisition.Acquired(ownership);
        }
        catch (IOException exception)
        {
            return WorkspaceCommitLockAcquisition.Failed(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return WorkspaceCommitLockAcquisition.Failed(exception.Message);
        }
    }
}
