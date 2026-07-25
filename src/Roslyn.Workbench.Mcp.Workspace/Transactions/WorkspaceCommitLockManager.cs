namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitLockManager : IWorkspaceCommitLockManager
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceFileLockProvider _fileLockProvider;
    private readonly IPhysicalPathContainment _pathContainment;

    public WorkspaceCommitLockManager(
        IFileSystem fileSystem,
        IWorkspaceFileLockProvider fileLockProvider,
        IPhysicalPathContainment pathContainment)
    {
        _fileSystem = fileSystem;
        _fileLockProvider = fileLockProvider;
        _pathContainment = pathContainment;
    }

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
