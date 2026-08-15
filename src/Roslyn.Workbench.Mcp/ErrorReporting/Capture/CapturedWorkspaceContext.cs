using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedWorkspaceContext
{
    public Guid WorkspaceId { get; }

    public long WorkspaceEpoch { get; }

    public string LifecycleState { get; }

    public int ProjectCount { get; }

    public int DocumentCount { get; }

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
