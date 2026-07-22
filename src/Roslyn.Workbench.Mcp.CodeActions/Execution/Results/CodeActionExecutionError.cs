namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

internal sealed record CodeActionExecutionError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? CorrelationId { get; init; }
}
