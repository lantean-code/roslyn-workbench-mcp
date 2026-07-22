namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed record FixAllActionCreationFailure
{
    public required string Message { get; init; }
}
