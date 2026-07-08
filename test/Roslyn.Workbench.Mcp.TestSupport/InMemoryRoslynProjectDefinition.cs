namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Defines one in-memory Roslyn project to be created by <see cref="InMemoryRoslynFactory"/>.
/// </summary>
public sealed record InMemoryRoslynProjectDefinition
{
    /// <summary>
    /// Gets the logical project name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional assembly name. When omitted, <see cref="Name"/> is used.
    /// </summary>
    public string? AssemblyName { get; init; }

    /// <summary>
    /// Gets the optional project file path.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the names of projects referenced by this project.
    /// </summary>
    public IReadOnlyList<string> ProjectReferences { get; init; } = [];

    /// <summary>
    /// Gets the documents included in this project.
    /// </summary>
    public IReadOnlyList<InMemoryRoslynDocumentDefinition> Documents { get; init; } = [];
}
