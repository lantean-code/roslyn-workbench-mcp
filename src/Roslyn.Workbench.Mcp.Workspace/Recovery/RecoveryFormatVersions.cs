namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Defines the durable recovery formats understood by this Host.
/// </summary>
internal static class RecoveryFormatVersions
{
    /// <summary>
    /// The recovery format introduced before the 1.0 compatibility baseline.
    /// </summary>
    public const int V1 = 1;

    /// <summary>
    /// The format written by the current Host.
    /// </summary>
    public const int Current = V1;

    /// <summary>
    /// Determines whether this Host has a reader for a persisted recovery format.
    /// </summary>
    /// <param name="version">The persisted format version.</param>
    /// <returns><see langword="true"/> when a format-specific reader is available; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupported(int version)
    {
        return version is V1;
    }
}
