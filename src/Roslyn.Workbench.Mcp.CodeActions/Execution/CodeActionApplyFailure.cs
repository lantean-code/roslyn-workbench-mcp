namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed record CodeActionApplyFailure
{
    public required CodeActionApplyFailureKind Kind { get; init; }

    public required string Message { get; init; }
}
