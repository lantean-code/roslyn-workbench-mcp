namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed record CodeActionSourceSelection
{
    public required Document Document { get; init; }

    public required TextSpan Span { get; init; }
}
