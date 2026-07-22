namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

internal sealed record CodeActionSourceSelection
{
    public required Document Document { get; init; }

    public required TextSpan Span { get; init; }
}
