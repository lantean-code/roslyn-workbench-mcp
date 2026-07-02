namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record ResolvedDocumentSpan
{
    public Document Document { get; init; } = null!;

    public TextSpan Span { get; init; }
}
