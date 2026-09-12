namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Defines stable machine-readable codes for durable recovery status.
/// </summary>
internal static class RecoveryStatusCodes
{
    /// <summary>
    /// The recovery evidence uses a format this Host cannot safely interpret.
    /// </summary>
    public const string VersionUnsupported = "RecoveryVersionUnsupported";
}
