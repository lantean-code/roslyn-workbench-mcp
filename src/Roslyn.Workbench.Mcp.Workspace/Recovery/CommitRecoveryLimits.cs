namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed class CommitRecoveryLimits
{
    private static readonly CommitRecoveryLimits _default = new(
        maximumOwnerBytes: 1024 * 1024,
        maximumLegacyStatusBytes: 1024 * 1024,
        maximumManifestBytes: 16 * 1024 * 1024,
        maximumArtifactBytes: 128 * 1024 * 1024);

    public static CommitRecoveryLimits Default => _default;

    public long MaximumOwnerBytes { get; }

    public long MaximumLegacyStatusBytes { get; }

    public long MaximumManifestBytes { get; }

    public long MaximumArtifactBytes { get; }

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
