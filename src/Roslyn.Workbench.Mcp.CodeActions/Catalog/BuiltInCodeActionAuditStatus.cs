namespace Roslyn.Workbench.Mcp.CodeActions.Catalog;

internal enum BuiltInCodeActionAuditStatus
{
    Unclassified,
    ValidatedSupported,
    PendingReplayValidation,
    RequiresBuiltInDiagnosticSupport,
    RequiresActionLevelClassification,
    CoveredByDedicatedTool,
    RequiresDedicatedImplementation,
    Excluded,
}
