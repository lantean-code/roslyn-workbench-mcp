namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents high-level project information.
/// </summary>
public sealed record ProjectInfo
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assembly name.
    /// </summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project language.
    /// </summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target frameworks inferred for the project.
    /// </summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];
}
