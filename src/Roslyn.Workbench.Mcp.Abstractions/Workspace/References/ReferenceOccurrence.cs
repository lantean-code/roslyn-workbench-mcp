namespace Roslyn.Workbench.Mcp.Workspace.References;

/// <summary>
/// Represents one normalized source occurrence discovered for a symbol.
/// </summary>
public sealed record ReferenceOccurrence
{
    /// <summary>
    /// Gets the Roslyn source location.
    /// </summary>
    public required Location Location { get; init; }

    /// <summary>
    /// Gets the document containing the source location.
    /// </summary>
    public required Document Document { get; init; }

    /// <summary>
    /// Gets the related definition symbol reported by Roslyn.
    /// </summary>
    public required ISymbol Definition { get; init; }

    /// <summary>
    /// Gets a value indicating whether this occurrence is a definition.
    /// </summary>
    public required bool IsDefinition { get; init; }
}
