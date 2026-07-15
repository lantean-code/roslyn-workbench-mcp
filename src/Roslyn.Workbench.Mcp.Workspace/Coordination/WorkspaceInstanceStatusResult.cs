namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed class WorkspaceInstanceStatusResult
{
    public static WorkspaceInstanceStatusResult Empty { get; } = new(true, false, false, []);

    public static WorkspaceInstanceStatusResult Unavailable { get; } = new(false, false, false, []);

    public bool IsAvailable { get; }

    public bool HasOtherLiveInstance { get; }

    public bool HasUnreadableLiveInstance { get; }

    public IReadOnlyList<WorkspaceInstanceInfo> Instances { get; }

    public WorkspaceInstanceStatusResult(
        bool isAvailable,
        bool hasOtherLiveInstance,
        bool hasUnreadableLiveInstance,
        IReadOnlyList<WorkspaceInstanceInfo> instances)
    {
        IsAvailable = isAvailable;
        HasOtherLiveInstance = hasOtherLiveInstance;
        HasUnreadableLiveInstance = hasUnreadableLiveInstance;
        Instances = instances;
    }
}
