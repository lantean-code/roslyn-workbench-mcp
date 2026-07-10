namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one direct project reference.
/// </summary>
public sealed record ProjectReferenceInfo
{
    /// <summary>
    /// Gets the referenced project identifier.
    /// </summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the referenced project name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the referenced project path.
    /// </summary>
    public string Path { get; init; } = string.Empty;
}
