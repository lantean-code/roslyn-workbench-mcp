using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Reports whether a commit recovery plan was persisted within the configured capacity limits.
/// </summary>
internal sealed record CommitRecoveryPlanPersistenceResult
{
    /// <summary>
    /// Gets a value indicating whether the plan was persisted.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsPersisted { get; }

    /// <summary>
    /// Gets the reason the plan could not be persisted.
    /// </summary>
    public string? ErrorMessage { get; }

    private CommitRecoveryPlanPersistenceResult(bool isPersisted, string? errorMessage)
    {
        IsPersisted = isPersisted;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful persistence result.
    /// </summary>
    /// <returns>A result that represents successful persistence.</returns>
    public static CommitRecoveryPlanPersistenceResult Persisted()
    {
        return new CommitRecoveryPlanPersistenceResult(isPersisted: true, errorMessage: null);
    }

    /// <summary>
    /// Creates a failed result for a plan that exceeds recovery storage capacity.
    /// </summary>
    /// <param name="errorMessage">The message that explains the failure.</param>
    /// <returns>A result that represents capacity exhaustion.</returns>
    public static CommitRecoveryPlanPersistenceResult CapacityExceeded(string errorMessage)
    {
        return new CommitRecoveryPlanPersistenceResult(isPersisted: false, errorMessage);
    }
}
