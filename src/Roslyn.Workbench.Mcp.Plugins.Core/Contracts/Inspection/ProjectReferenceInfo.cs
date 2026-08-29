namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one direct project reference.
/// </summary>
internal sealed record ProjectReferenceInfo
{
    /// <summary>
    /// Gets the referenced project identifier.
    /// </summary>
    [Description("The referenced project identifier.")]
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the referenced project name.
    /// </summary>
    [Description("The referenced project name.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the referenced project path.
    /// </summary>
    [Description("The referenced project path.")]
    public string Path { get; init; } = string.Empty;
}
