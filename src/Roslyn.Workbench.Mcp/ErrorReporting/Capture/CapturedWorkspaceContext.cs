using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Captures the non-sensitive workspace state in which a tool failure occurred.
/// </summary>
internal sealed record CapturedWorkspaceContext
{
    /// <summary>
    /// Server-generated identifier of the workspace active during the failure.
    /// </summary>
    [Description("Server-generated identifier of the workspace active during the failure.")]
    public Guid WorkspaceId { get; }

    /// <summary>
    /// Workspace epoch active during the failure.
    /// </summary>
    [Description("Workspace epoch active during the failure.")]
    public long WorkspaceEpoch { get; }

    /// <summary>
    /// Workspace lifecycle state at the time of failure.
    /// </summary>
    [Description("Workspace lifecycle state at the time of failure.")]
    public string LifecycleState { get; }

    /// <summary>
    /// Number of loaded projects at the time of failure.
    /// </summary>
    [Description("Number of loaded projects at the time of failure.")]
    public int ProjectCount { get; }

    /// <summary>
    /// Number of loaded documents at the time of failure.
    /// </summary>
    [Description("Number of loaded documents at the time of failure.")]
    public int DocumentCount { get; }

    /// <summary>
    /// Active transaction revision at the time of failure, when applicable.
    /// </summary>
    [Description("Active transaction revision at the time of failure, when applicable.")]
    public int? TransactionRevision { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CapturedWorkspaceContext"/> class.
    /// </summary>
    /// <param name="workspaceIdentity">The identity of the workspace being processed.</param>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="transactionRevision">The active transaction revision, or <see langword="null"/> for a query against the ready workspace.</param>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="CapturedWorkspaceContext"/> class.
    /// </summary>
    /// <param name="workspaceIdentity">The identity of the workspace being processed.</param>
    /// <param name="lifecycleState">The workspace lifecycle state represented by the captured context.</param>
    /// <param name="projectCount">The number of projects represented by the captured workspace state.</param>
    /// <param name="documentCount">The number of documents represented by the captured workspace state.</param>
    /// <param name="transactionRevision">The active transaction revision, when the captured state represents a transaction.</param>
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
