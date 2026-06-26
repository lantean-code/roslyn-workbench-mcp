namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents one metadata reference for a project.
/// </summary>
public sealed record MetadataReferenceInfo
{
    /// <summary>
    /// Gets the display string for the metadata reference.
    /// </summary>
    public string Display { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file path, when available.
    /// </summary>
    public string? Path { get; init; }
}
