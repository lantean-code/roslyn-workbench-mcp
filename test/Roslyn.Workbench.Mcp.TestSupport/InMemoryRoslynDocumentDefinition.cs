namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Defines one in-memory Roslyn document to be created by <see cref="InMemoryRoslynFactory"/>.
/// </summary>
public sealed record InMemoryRoslynDocumentDefinition
{
    /// <summary>
    /// Gets the logical document name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional document file path.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the source text to load into the document.
    /// </summary>
    public string Source { get; init; } = string.Empty;
}
