namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Couples a durable commit manifest with the recovery artifacts it references.
/// </summary>
internal sealed record WorkspaceCommitPlan
{
    /// <summary>
    /// Gets the ordered and validated file-operation manifest.
    /// </summary>
    public WorkspaceCommitManifest Manifest { get; }

    /// <summary>
    /// Gets the original, intended, and deletion-marker artifacts keyed by recovery path.
    /// </summary>
    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Artifacts { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceCommitPlan"/> class.
    /// </summary>
    /// <param name="manifest">The ordered and validated file-operation manifest.</param>
    /// <param name="artifacts">The recovery artifacts to expose through an in-memory file system.</param>
    public WorkspaceCommitPlan(
        WorkspaceCommitManifest manifest,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> artifacts)
    {
        Manifest = manifest;
        Artifacts = artifacts;
    }
}
