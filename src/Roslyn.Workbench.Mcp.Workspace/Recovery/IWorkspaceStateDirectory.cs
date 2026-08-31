namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Provides the secured directories used for durable Workspace state.
/// </summary>
internal interface IWorkspaceStateDirectory
{
    /// <summary>
    /// Gets the root directory for durable Workspace state.
    /// </summary>
    string RootDirectory { get; }

    /// <summary>
    /// Gets the directory containing commit recovery evidence.
    /// </summary>
    string RecoveryDirectory { get; }

    /// <summary>
    /// Creates and validates the state directory structure.
    /// </summary>
    void Initialize();
}
