namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed record CodeActionToolMetadata
{
    public string Name { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? ResultSummary { get; init; }

    public CodeActionToolBehavior Behavior { get; init; } = new();
}
