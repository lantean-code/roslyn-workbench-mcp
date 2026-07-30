namespace Roslyn.Workbench.Mcp.Workspace.References;

internal readonly record struct ReferenceOccurrenceIdentity
{
    public required DocumentId DocumentId { get; init; }

    public required TextSpan Span { get; init; }

    public required bool IsDefinition { get; init; }

    public required string? DefinitionId { get; init; }
}
