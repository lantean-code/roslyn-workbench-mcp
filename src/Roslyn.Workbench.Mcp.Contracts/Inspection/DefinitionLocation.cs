using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents one definition result, either in source or metadata.
/// </summary>
public sealed record DefinitionLocation
{
    /// <summary>
    /// Gets the source location, when the definition is in source.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether the definition is metadata-only.
    /// </summary>
    public bool IsMetadata { get; init; }

    /// <summary>
    /// Gets the metadata symbol name, when the definition is metadata-only.
    /// </summary>
    public string? MetadataName { get; init; }

    /// <summary>
    /// Gets the containing assembly display name, when available.
    /// </summary>
    public string? ContainingAssembly { get; init; }
}
