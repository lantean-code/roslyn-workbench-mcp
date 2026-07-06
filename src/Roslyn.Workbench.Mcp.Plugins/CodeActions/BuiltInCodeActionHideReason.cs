namespace Roslyn.Workbench.Mcp.Plugins.CodeActions;

internal enum BuiltInCodeActionHideReason
{
    None,
    ReplayProofFailed,
    IntentionallyDeferred,
    ImpossibleUnderCurrentRules,
}
