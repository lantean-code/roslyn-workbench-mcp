namespace Roslyn.Workbench.Mcp.Workspace.References;

/// <summary>
/// Provides a stable identity for a symbol definition or reference occurrence.
/// </summary>
internal readonly record struct ReferenceOccurrenceIdentity
{
    /// <summary>
    /// Gets the Roslyn document containing the occurrence.
    /// </summary>
    public required DocumentId DocumentId { get; init; }

    /// <summary>
    /// Gets the source span of the occurrence.
    /// </summary>
    public required TextSpan Span { get; init; }

    /// <summary>
    /// Gets a value indicating whether the occurrence is the symbol definition.
    /// </summary>
    public required bool IsDefinition { get; init; }

    /// <summary>
    /// Gets the Roslyn definition identifier shared by related occurrences.
    /// </summary>
    public required string? DefinitionId { get; init; }
}
