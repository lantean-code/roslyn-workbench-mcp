namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed class WorkspaceInstanceScanResult
{
    public static WorkspaceInstanceScanResult Empty { get; } = new(false, []);

    public bool HasOtherLiveInstance { get; }

    public IReadOnlyList<WorkspaceInstanceInfo> Instances { get; }

    public WorkspaceInstanceScanResult(
        bool hasOtherLiveInstance,
        IReadOnlyList<WorkspaceInstanceInfo> instances)
    {
        HasOtherLiveInstance = hasOtherLiveInstance;
        Instances = instances;
    }
}
