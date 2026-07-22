namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

internal sealed record FixAllActionCreationFailure
{
    public required string Message { get; init; }
}
