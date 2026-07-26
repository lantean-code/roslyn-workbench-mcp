namespace Roslyn.Workbench.Mcp.CodeActions.Catalog;

internal sealed record BuiltInCodeActionFamily
{
    public string ProviderId { get; init; } = string.Empty;

    public string? ToolName { get; init; }

    public BuiltInCodeActionFamilyKind Kind { get; init; } = BuiltInCodeActionFamilyKind.Refactoring;

    public required CodeActionExecutionMode ExecutionMode { get; init; }

    public string? ExecutorTool { get; init; }

}
