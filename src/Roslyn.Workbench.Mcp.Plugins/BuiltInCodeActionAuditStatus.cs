namespace Roslyn.Workbench.Mcp.Plugins;

internal enum BuiltInCodeActionAuditStatus
{
    ValidatedSupported,
    ValidationCandidate,
    Deferred,
    ImpossibleUnderCurrentRules,
}
