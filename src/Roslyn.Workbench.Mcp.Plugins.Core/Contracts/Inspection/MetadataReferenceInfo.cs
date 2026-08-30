namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one metadata reference for a project.
/// </summary>
internal sealed record MetadataReferenceInfo
{
    /// <summary>
    /// Gets the display string for the metadata reference.
    /// </summary>
    [Description("The display string for the metadata reference.")]
    public required string Display { get; init; }

    /// <summary>
    /// Gets the file path, when available.
    /// </summary>
    [Description("The file path, when available.")]
    public string? Path { get; init; }
}
