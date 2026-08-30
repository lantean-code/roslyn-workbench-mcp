namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents high-level project information.
/// </summary>
internal sealed record ProjectInfo
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    [Description("The project identifier.")]
    public required string ProjectId { get; init; }

    /// <summary>
    /// Gets the project name.
    /// </summary>
    [Description("The project name.")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the project path.
    /// </summary>
    [Description("The project path.")]
    public required string Path { get; init; }

    /// <summary>
    /// Gets the assembly name.
    /// </summary>
    [Description("The assembly name, when available.")]
    public string? AssemblyName { get; init; }

    /// <summary>
    /// Gets the project language.
    /// </summary>
    [Description("The project language.")]
    public required string Language { get; init; }

    /// <summary>
    /// Gets the target frameworks inferred for the project.
    /// </summary>
    [Description("The target frameworks inferred for the project.")]
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];
}
