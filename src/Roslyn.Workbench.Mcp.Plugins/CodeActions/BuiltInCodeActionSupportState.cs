namespace Roslyn.Workbench.Mcp.Plugins.CodeActions;

internal enum BuiltInCodeActionSupportState
{
    SupportedReplay,
    SupportedParameterised,
    HiddenImpossibleUnderCurrentRules,
    HiddenIntentionallyDeferred,
}
