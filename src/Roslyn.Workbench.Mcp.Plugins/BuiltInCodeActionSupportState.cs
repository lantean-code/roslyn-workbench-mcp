namespace Roslyn.Workbench.Mcp.Plugins;

internal enum BuiltInCodeActionSupportState
{
    SupportedReplay,
    SupportedParameterised,
    HiddenImpossibleUnderCurrentRules,
    HiddenIntentionallyDeferred,
}
