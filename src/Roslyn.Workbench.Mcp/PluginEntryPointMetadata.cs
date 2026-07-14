namespace Roslyn.Workbench.Mcp;

internal sealed record PluginEntryPointMetadata
{
    public string PluginId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string SupportedApiVersion { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string EntryTypeName { get; init; } = string.Empty;
}
