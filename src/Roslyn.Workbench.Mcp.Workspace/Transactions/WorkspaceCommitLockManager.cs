namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitLockManager : IWorkspaceCommitLockManager
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceFileLockProvider _fileLockProvider;

    public WorkspaceCommitLockManager(IFileSystem fileSystem, IWorkspaceFileLockProvider fileLockProvider)
    {
        _fileSystem = fileSystem;
        _fileLockProvider = fileLockProvider;
    }

    public WorkspaceCommitLockAcquisition Acquire(string workspaceRoot)
    {
        try
        {
            var canonicalRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
            var directory = _fileSystem.Path.Combine(canonicalRoot, ".vs", "roslyn-workbench-mcp", "locks");
            _fileSystem.Directory.CreateDirectory(directory);
            var ownership = _fileLockProvider.TryAcquire(_fileSystem.Path.Combine(directory, "commit.lock"));
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
