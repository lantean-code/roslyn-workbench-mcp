using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceCommitPlanResult
{
    public WorkspaceCommitPlan? Plan { get; }

    public string? ErrorMessage { get; }

    [MemberNotNullWhen(true, nameof(Plan))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSucceeded => Plan is not null;

    private WorkspaceCommitPlanResult(WorkspaceCommitPlan? plan, string? errorMessage)
    {
        Plan = plan;
        ErrorMessage = errorMessage;
    }

    public static WorkspaceCommitPlanResult Succeeded(WorkspaceCommitPlan plan)
    {
        return new WorkspaceCommitPlanResult(plan, errorMessage: null);
    }

    public static WorkspaceCommitPlanResult Failed(string errorMessage)
    {
        return new WorkspaceCommitPlanResult(plan: null, errorMessage);
    }
}
