namespace Roslyn.Workbench.Mcp.Plugins;

internal enum BuiltInCodeActionHideReason
{
    None,
    ReplayProofFailed,
    IntentionallyDeferred,
    ImpossibleUnderCurrentRules,
}
