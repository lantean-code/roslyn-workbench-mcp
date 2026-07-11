namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceCommitPlan
{
    public WorkspaceCommitManifest Manifest { get; }

    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Artifacts { get; }

    public WorkspaceCommitPlan(
        WorkspaceCommitManifest manifest,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> artifacts)
    {
        Manifest = manifest;
        Artifacts = artifacts;
    }
}
