namespace Roslyn.Workbench.Mcp.CodeActions.Catalog;

internal sealed record BuiltInCodeActionFamily
{
    public string ProviderId { get; init; } = string.Empty;

    public string? ToolName { get; init; }

    public BuiltInCodeActionFamilyKind Kind { get; init; } = BuiltInCodeActionFamilyKind.Refactoring;

    public BuiltInCodeActionSupportState State { get; init; }

    public BuiltInCodeActionAuditStatus AuditStatus { get; init; } = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules;

    public BuiltInCodeActionHideReason HideReason { get; init; }

    public string? ExecutorTool { get; init; }

    public CodeActionExecutionMode ExecutionMode =>
        State switch
        {
            BuiltInCodeActionSupportState.SupportedReplay => CodeActionExecutionMode.Replay,
            BuiltInCodeActionSupportState.SupportedParameterised => CodeActionExecutionMode.Parameterised,
            _ => CodeActionExecutionMode.Unsupported,
        };

    public bool IsVisible => State is BuiltInCodeActionSupportState.SupportedReplay or BuiltInCodeActionSupportState.SupportedParameterised;

    public bool IsDedicatedToolVisible => !string.IsNullOrWhiteSpace(ToolName) && IsVisible;
}
