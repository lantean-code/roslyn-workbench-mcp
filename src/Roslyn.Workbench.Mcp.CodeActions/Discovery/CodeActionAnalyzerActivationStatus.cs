namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal enum CodeActionAnalyzerActivationStatus
{
    Available,
    TypeNotFound,
    IncompatibleType,
    InspectionFailed,
    ConstructionFailed,
}
