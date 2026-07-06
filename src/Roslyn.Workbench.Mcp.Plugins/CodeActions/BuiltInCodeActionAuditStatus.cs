namespace Roslyn.Workbench.Mcp.Plugins.CodeActions;

internal enum BuiltInCodeActionAuditStatus
{
    ValidatedSupported,
    ValidationCandidate,
    Deferred,
    ImpossibleUnderCurrentRules,
}
