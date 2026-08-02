using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed record CommitRecoveryPlanPersistenceResult
{
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsPersisted { get; }

    public string? ErrorMessage { get; }

    private CommitRecoveryPlanPersistenceResult(bool isPersisted, string? errorMessage)
    {
        IsPersisted = isPersisted;
        ErrorMessage = errorMessage;
    }

    public static CommitRecoveryPlanPersistenceResult Persisted()
    {
        return new CommitRecoveryPlanPersistenceResult(isPersisted: true, errorMessage: null);
    }

    public static CommitRecoveryPlanPersistenceResult CapacityExceeded(string errorMessage)
    {
        return new CommitRecoveryPlanPersistenceResult(isPersisted: false, errorMessage);
    }
}
