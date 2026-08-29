namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one definition result, either in source or metadata.
/// </summary>
internal sealed record DefinitionLocation
{
    /// <summary>
    /// Gets the source location, when the definition is in source.
    /// </summary>
    [Description("The source location, when the definition is in source.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether the definition is metadata-only.
    /// </summary>
    [Description("Whether the definition is metadata-only.")]
    public bool IsMetadata { get; init; }

    /// <summary>
    /// Gets the metadata symbol name, when the definition is metadata-only.
    /// </summary>
    [Description("The metadata symbol name, when the definition is metadata-only.")]
    public string? MetadataName { get; init; }

    /// <summary>
    /// Gets the containing assembly display name, when available.
    /// </summary>
    [Description("The containing assembly display name, when available.")]
    public string? ContainingAssembly { get; init; }
}
