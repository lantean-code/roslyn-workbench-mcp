namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Finds generated-looking checked-in source affected by a candidate solution change.
/// </summary>
internal interface IGeneratedSourceMutationInspector
{
    /// <summary>
    /// Inspects the regular source documents changed between two solution snapshots.
    /// </summary>
    /// <param name="baselineSolution">The solution before the change.</param>
    /// <param name="candidateSolution">The solution after the change.</param>
    /// <param name="workspaceRoot">The Workspace root used to create stable display paths.</param>
    /// <param name="cancellationToken">The token used to cancel source inspection.</param>
    /// <returns>The distinct generated-looking Workspace-relative paths in ordinal order.</returns>
    ValueTask<IReadOnlyList<string>> FindGeneratedSourcePathsAsync(
        Solution baselineSolution,
        Solution candidateSolution,
        string workspaceRoot,
        CancellationToken cancellationToken);
}
