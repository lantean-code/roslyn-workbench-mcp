namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Verifies that source documents outside the writable Workspace root still match their loaded Roslyn text.
/// </summary>
internal interface IWorkspaceReadOnlyDocumentValidator
{
    /// <summary>
    /// Validates every file-backed document outside the Workspace root.
    /// </summary>
    /// <param name="solution">The solution containing the loaded document snapshots.</param>
    /// <param name="workspaceRoot">The writable Workspace boundary.</param>
    /// <param name="cancellationToken">Cancels document validation.</param>
    /// <returns>The aggregate validation status.</returns>
    ValueTask<WorkspaceReadOnlyDocumentValidationStatus> ValidateAsync(
        Solution solution,
        string workspaceRoot,
        CancellationToken cancellationToken);
}
