namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Defines size limits for persisted commit-recovery records and artifacts.
/// </summary>
internal sealed class CommitRecoveryLimits
{
    private static readonly CommitRecoveryLimits _default = new(
        maximumOwnerBytes: 1024 * 1024,
        maximumLegacyStatusBytes: 1024 * 1024,
        maximumManifestBytes: 16 * 1024 * 1024,
        maximumArtifactBytes: 128 * 1024 * 1024);

    /// <summary>
    /// Gets the default recovery persistence limits.
    /// </summary>
    public static CommitRecoveryLimits Default => _default;

    /// <summary>
    /// Gets the maximum encoded size of a commit owner record.
    /// </summary>
    public long MaximumOwnerBytes { get; }

    /// <summary>
    /// Gets the maximum encoded size of a legacy recovery status record.
    /// </summary>
    public long MaximumLegacyStatusBytes { get; }

    /// <summary>
    /// Gets the maximum encoded size of a recovery manifest.
    /// </summary>
    public long MaximumManifestBytes { get; }

    /// <summary>
    /// Gets the maximum size of an individual recovery artifact.
    /// </summary>
    public long MaximumArtifactBytes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommitRecoveryLimits"/> class.
    /// </summary>
    /// <param name="maximumOwnerBytes">The maximum permitted size of a workspace ownership record.</param>
    /// <param name="maximumLegacyStatusBytes">The maximum permitted size of a legacy status record.</param>
    /// <param name="maximumManifestBytes">The maximum permitted size of a recovery manifest.</param>
    /// <param name="maximumArtifactBytes">The maximum permitted size of an individual recovery artifact.</param>
    public CommitRecoveryLimits(
        long maximumOwnerBytes,
        long maximumLegacyStatusBytes,
        long maximumManifestBytes,
        long maximumArtifactBytes)
    {
        MaximumOwnerBytes = maximumOwnerBytes;
        MaximumLegacyStatusBytes = maximumLegacyStatusBytes;
        MaximumManifestBytes = maximumManifestBytes;
        MaximumArtifactBytes = maximumArtifactBytes;
    }
}
