namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Captures non-sensitive workspace state for diagnosing an unexpected operation failure.
/// </summary>
internal sealed record WorkspaceFailureContext
{
    /// <summary>
    /// Gets the identity and snapshot of the workspace in which the failure occurred.
    /// </summary>
    public WorkspaceIdentity Workspace { get; }

    /// <summary>
    /// Gets the workspace lifecycle state at the time of failure.
    /// </summary>
    public WorkspaceLifecycleState LifecycleState { get; }

    /// <summary>
    /// Gets the number of projects visible to the failed operation.
    /// </summary>
    public int ProjectCount { get; }

    /// <summary>
    /// Gets the number of documents visible to the failed operation.
    /// </summary>
    public int DocumentCount { get; }

    /// <summary>
    /// Gets the active transaction revision at the time of failure, when one existed.
    /// </summary>
    public int? TransactionRevision { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceFailureContext"/> class.
    /// </summary>
    /// <param name="workspace">The workspace identity and snapshot represented by the failure context.</param>
    /// <param name="lifecycleState">The workspace lifecycle state represented by the captured context.</param>
    /// <param name="projectCount">The number of projects represented by the captured workspace state.</param>
    /// <param name="documentCount">The number of documents represented by the captured workspace state.</param>
    /// <param name="transactionRevision">The active transaction revision, when one existed.</param>
    public WorkspaceFailureContext(
        WorkspaceIdentity workspace,
        WorkspaceLifecycleState lifecycleState,
        int projectCount,
        int documentCount,
        int? transactionRevision)
    {
        Workspace = workspace;
        LifecycleState = lifecycleState;
        ProjectCount = projectCount;
        DocumentCount = documentCount;
        TransactionRevision = transactionRevision;
    }
}
