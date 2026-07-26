namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal sealed record BuiltInCodeActionProviderAssessmentEntry
{
    public string ProviderId { get; init; } = string.Empty;

    public BuiltInCodeActionFamilyKind Kind { get; init; }

    public BuiltInCodeActionAuditStatus Status { get; init; }
}
