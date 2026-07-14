namespace Roslyn.Workbench.Mcp;

internal sealed record PluginPackageCandidate
{
    public string PackageDirectory { get; init; } = string.Empty;

    public string EntryAssemblyPath { get; init; } = string.Empty;

    public PluginEntryPointMetadata EntryPoint { get; init; } = new();
}
