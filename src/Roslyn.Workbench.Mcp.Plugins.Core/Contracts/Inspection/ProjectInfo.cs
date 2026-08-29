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
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project name.
    /// </summary>
    [Description("The project name.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project path.
    /// </summary>
    [Description("The project path.")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assembly name.
    /// </summary>
    [Description("The assembly name.")]
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project language.
    /// </summary>
    [Description("The project language.")]
    public string Language { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target frameworks inferred for the project.
    /// </summary>
    [Description("The target frameworks inferred for the project.")]
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];
}
