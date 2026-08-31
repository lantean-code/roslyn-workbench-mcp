namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Indicates whether read-only documents outside the Workspace root still match their loaded snapshots.
/// </summary>
internal enum WorkspaceReadOnlyDocumentValidationStatus
{
    /// <summary>
    /// Every external document remains unchanged and readable.
    /// </summary>
    Valid,

    /// <summary>
    /// At least one external document changed, disappeared or could not be validated.
    /// </summary>
    Invalid,
}
