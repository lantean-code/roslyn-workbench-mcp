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
    public required string ProjectId { get; init; }

    /// <summary>
    /// Gets the referenced project name.
    /// </summary>
    [Description("The referenced project name.")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the referenced project path.
    /// </summary>
    [Description("The referenced project path.")]
    public required string Path { get; init; }
}
