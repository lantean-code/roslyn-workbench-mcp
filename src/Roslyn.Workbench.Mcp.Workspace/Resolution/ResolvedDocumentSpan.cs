namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed record ResolvedDocumentSpan
{
    public required Document Document { get; init; }

    public TextSpan Span { get; init; }
}
