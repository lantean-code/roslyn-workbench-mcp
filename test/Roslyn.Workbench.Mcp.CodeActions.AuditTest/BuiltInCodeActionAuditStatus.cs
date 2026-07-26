namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal enum BuiltInCodeActionAuditStatus
{
    ValidatedSupported,
    PendingReplayValidation,
    RequiresBuiltInDiagnosticSupport,
    RequiresActionLevelClassification,
    CoveredByDedicatedTool,
    RequiresDedicatedImplementation,
    Excluded,
}
