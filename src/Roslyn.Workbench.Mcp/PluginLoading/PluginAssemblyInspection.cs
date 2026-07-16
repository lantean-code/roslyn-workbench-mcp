namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PluginAssemblyInspection
{
    public bool IsManagedAssembly { get; init; }

    public IReadOnlyList<PluginEntryPointMetadata> EntryPoints { get; init; } = [];

    public string? Error { get; init; }
}
