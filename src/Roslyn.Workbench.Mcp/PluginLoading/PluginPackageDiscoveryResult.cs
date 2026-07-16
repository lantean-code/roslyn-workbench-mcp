namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PluginPackageDiscoveryResult
{
    public string FallbackIdentity { get; init; } = string.Empty;

    public PluginPackageCandidate? Candidate { get; init; }

    public string? Error { get; init; }
}
