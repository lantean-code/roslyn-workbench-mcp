namespace Roslyn.Workbench.Mcp.CodeActions.Catalog;

internal enum BuiltInCodeActionHideReason
{
    None,
    ReplayProofFailed,
    IntentionallyDeferred,
    ImpossibleUnderCurrentRules,
}
