namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

internal sealed record CodeActionExecutionFailure
{
    public CodeActionExecutionOutcome Outcome { get; init; }

    public CodeActionExecutionError Error { get; init; } = new();

    public RequiredAction? RequiredAction { get; init; }
}
