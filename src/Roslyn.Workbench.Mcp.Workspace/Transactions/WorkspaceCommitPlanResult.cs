using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a validated commit plan or the reason planning failed.
/// </summary>
internal sealed record WorkspaceCommitPlanResult
{
    /// <summary>
    /// Gets the validated commit plan when planning succeeds.
    /// </summary>
    public WorkspaceCommitPlan? Plan { get; }

    /// <summary>
    /// Gets the planning failure when no safe plan could be produced.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets a value indicating whether planning produced a commit plan.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Plan))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSucceeded => Plan is not null;

    private WorkspaceCommitPlanResult(WorkspaceCommitPlan? plan, string? errorMessage)
    {
        Plan = plan;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    /// <param name="plan">The commit or recovery plan produced by the preceding operation.</param>
    /// <returns>A result that represents successful completion.</returns>
    public static WorkspaceCommitPlanResult Succeeded(WorkspaceCommitPlan plan)
    {
        return new WorkspaceCommitPlanResult(plan, errorMessage: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="errorMessage">The message that explains the failure.</param>
    /// <returns>A result that represents failure.</returns>
    public static WorkspaceCommitPlanResult Failed(string errorMessage)
    {
        return new WorkspaceCommitPlanResult(plan: null, errorMessage);
    }
}
