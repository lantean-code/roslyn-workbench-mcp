namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

/// <summary>
/// Reports whether a workspace is available and which competing live instances were observed.
/// </summary>
internal sealed class WorkspaceInstanceStatusResult
{
    /// <summary>
    /// Gets an available status with no competing workspace instances.
    /// </summary>
    public static WorkspaceInstanceStatusResult Empty { get; } = new(true, false, false, []);

    /// <summary>
    /// Gets a status indicating that workspace instance information is unavailable.
    /// </summary>
    public static WorkspaceInstanceStatusResult Unavailable { get; } = new(false, false, false, []);

    /// <summary>
    /// Gets a value indicating whether the requested capability is available.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets a value indicating whether another live server instance owns the workspace.
    /// </summary>
    public bool HasOtherLiveInstance { get; }

    /// <summary>
    /// Gets a value indicating whether a live instance record could not be read.
    /// </summary>
    public bool HasUnreadableLiveInstance { get; }

    /// <summary>
    /// Gets the live workspace instance records observed during the status check.
    /// </summary>
    public IReadOnlyList<WorkspaceInstanceInfo> Instances { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceInstanceStatusResult"/> class.
    /// </summary>
    /// <param name="isAvailable">Whether the workspace or capability is available for use.</param>
    /// <param name="hasOtherLiveInstance">Whether another live server instance owns the workspace.</param>
    /// <param name="hasUnreadableLiveInstance">Whether a live workspace instance record could not be read.</param>
    /// <param name="instances">The live workspace instance records included in the status result.</param>
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
