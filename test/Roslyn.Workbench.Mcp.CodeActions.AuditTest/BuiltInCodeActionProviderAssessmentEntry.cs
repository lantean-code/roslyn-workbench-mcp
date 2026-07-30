namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal sealed record BuiltInCodeActionProviderAssessmentEntry
{
    public required string ProviderId { get; init; }

    public DiscoveredActionKind Kind { get; init; }

    public BuiltInCodeActionAuditStatus Status { get; init; }
}
