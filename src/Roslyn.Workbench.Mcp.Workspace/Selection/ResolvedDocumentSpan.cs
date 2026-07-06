namespace Roslyn.Workbench.Mcp.Workspace.Selection;

internal sealed record ResolvedDocumentSpan
{
    public Document Document { get; init; } = null!;

    public TextSpan Span { get; init; }
}
