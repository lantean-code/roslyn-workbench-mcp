namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

internal sealed record CodeActionApplyFailure
{
    public required CodeActionApplyFailureKind Kind { get; init; }

    public required string Message { get; init; }
}
