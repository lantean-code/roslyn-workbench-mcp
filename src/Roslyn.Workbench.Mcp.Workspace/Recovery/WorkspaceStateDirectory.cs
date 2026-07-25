using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed class WorkspaceStateDirectory : IWorkspaceStateDirectory
{
    private const string _recoveryDirectoryName = "recovery";

    private readonly IWorkspaceStateDirectorySecurity _security;

    public string RootDirectory { get; }

    public string RecoveryDirectory { get; }

    public WorkspaceStateDirectory(
        IOptions<WorkspaceOptions> options,
        IFileSystem fileSystem,
        IWorkspaceStateDirectorySecurity security)
    {
        _security = security;
        RootDirectory = fileSystem.Path.GetFullPath(options.Value.StateDirectory);
        RecoveryDirectory = fileSystem.Path.Combine(RootDirectory, _recoveryDirectoryName);
    }

    public void Initialize()
    {
        _security.EnsureDirectory(RootDirectory);
        _security.EnsureDirectory(RecoveryDirectory);
    }
}
