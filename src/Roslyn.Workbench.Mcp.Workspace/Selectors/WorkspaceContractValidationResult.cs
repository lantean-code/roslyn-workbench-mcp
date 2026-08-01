namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed record WorkspaceContractValidationResult
{
    private static readonly WorkspaceContractValidationResult _validResult = new(true, []);

    public bool IsValid { get; }

    public IReadOnlyList<WorkspaceContractValidationFailure> Failures { get; }

    private WorkspaceContractValidationResult(
        bool isValid,
        IReadOnlyList<WorkspaceContractValidationFailure> failures)
    {
        IsValid = isValid;
        Failures = failures;
    }

    public static WorkspaceContractValidationResult Valid()
    {
        return _validResult;
    }

    public static WorkspaceContractValidationResult Invalid(
        IReadOnlyList<WorkspaceContractValidationFailure> failures)
    {
        if (failures.Count == 0)
        {
            throw new ArgumentException("An invalid validation result must contain at least one failure.", nameof(failures));
        }

        var capturedFailures = failures.ToArray();
        return new WorkspaceContractValidationResult(false, capturedFailures);
    }
}
