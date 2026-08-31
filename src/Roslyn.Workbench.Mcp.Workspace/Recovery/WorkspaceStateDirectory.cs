using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Resolves and initializes the directories used for durable Workspace state.
/// </summary>
internal sealed class WorkspaceStateDirectory : IWorkspaceStateDirectory
{
    private const string _recoveryDirectoryName = "recovery";

    private readonly IWorkspaceStateDirectorySecurity _security;

    /// <inheritdoc/>
    public string RootDirectory { get; }

    /// <inheritdoc/>
    public string RecoveryDirectory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceStateDirectory"/> class.
    /// </summary>
    /// <param name="options">The options that control the operation.</param>
    /// <param name="fileSystem">The file-system abstraction used for storage operations.</param>
    /// <param name="security">The component that applies access controls to the workspace state directory.</param>
    public WorkspaceStateDirectory(
        IOptions<WorkspaceOptions> options,
        IFileSystem fileSystem,
        IWorkspaceStateDirectorySecurity security)
    {
        _security = security;
        RootDirectory = fileSystem.Path.GetFullPath(options.Value.StateDirectory);
        RecoveryDirectory = fileSystem.Path.Combine(RootDirectory, _recoveryDirectoryName);
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        _security.EnsureDirectory(RootDirectory);
        _security.EnsureDirectory(RecoveryDirectory);
        _security.ValidateWritableDirectory(RecoveryDirectory);
    }
}
