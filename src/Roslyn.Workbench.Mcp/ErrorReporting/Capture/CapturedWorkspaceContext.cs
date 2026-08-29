using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedWorkspaceContext
{
    /// <summary>
    /// Gets the Workspace Id.
    /// </summary>
    [Description("Server-generated identifier of the workspace active during the failure.")]
    public Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the Workspace Epoch.
    /// </summary>
    [Description("Workspace epoch active during the failure.")]
    public long WorkspaceEpoch { get; }

    /// <summary>
    /// Gets the Lifecycle State.
    /// </summary>
    [Description("Workspace lifecycle state at the time of failure.")]
    public string LifecycleState { get; }

    /// <summary>
    /// Gets the Project Count.
    /// </summary>
    [Description("Number of loaded projects at the time of failure.")]
    public int ProjectCount { get; }

    /// <summary>
    /// Gets the Document Count.
    /// </summary>
    [Description("Number of loaded documents at the time of failure.")]
    public int DocumentCount { get; }

    /// <summary>
    /// Gets the Transaction Revision.
    /// </summary>
    [Description("Active transaction revision at the time of failure, when applicable.")]
    public int? TransactionRevision { get; }

    public CapturedWorkspaceContext(
        WorkspaceIdentity workspaceIdentity,
        Solution currentSolution,
        int? transactionRevision)
        : this(
            workspaceIdentity,
            GetAcquiredExecutionLifecycleState(transactionRevision),
            currentSolution.ProjectIds.Count,
            GetDocumentCount(currentSolution),
            transactionRevision)
    {
    }

    public CapturedWorkspaceContext(
        WorkspaceIdentity workspaceIdentity,
        WorkspaceLifecycleState lifecycleState,
        int projectCount,
        int documentCount,
        int? transactionRevision)
    {
        WorkspaceId = workspaceIdentity.WorkspaceId;
        WorkspaceEpoch = workspaceIdentity.WorkspaceEpoch;
        LifecycleState = lifecycleState.ToString();
        ProjectCount = projectCount;
        DocumentCount = documentCount;
        TransactionRevision = transactionRevision;
    }

    private static WorkspaceLifecycleState GetAcquiredExecutionLifecycleState(int? transactionRevision)
    {
        return transactionRevision is null
            ? WorkspaceLifecycleState.Ready
            : WorkspaceLifecycleState.TransactionActive;
    }

    private static int GetDocumentCount(Solution currentSolution)
    {
        return currentSolution.Projects.Sum(static project => project.Documents.Count());
    }
}
