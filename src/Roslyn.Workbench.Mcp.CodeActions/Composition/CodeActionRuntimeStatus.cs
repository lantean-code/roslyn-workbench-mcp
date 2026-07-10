namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record CodeActionRuntimeStatus
{
    public bool IsAvailable { get; init; }

    public string? Version { get; init; }

    public string? Message { get; init; }
}
