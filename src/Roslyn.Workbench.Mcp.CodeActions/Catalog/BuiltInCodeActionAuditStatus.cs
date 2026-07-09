namespace Roslyn.Workbench.Mcp.CodeActions.Catalog;

internal enum BuiltInCodeActionAuditStatus
{
    ValidatedSupported,
    ValidationCandidate,
    Deferred,
    ImpossibleUnderCurrentRules,
}
