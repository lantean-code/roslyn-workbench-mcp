namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal enum CodeActionInfoCreationStatus
{
    Succeeded,
    LocationUnavailable,
    DocumentPathUnavailable,
    ReferenceCapacityExceeded,
}
