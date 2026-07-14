using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceCommitValidationResult
{
    private static readonly WorkspaceCommitValidationResult _valid = new(isValid: true, errorMessage: null);

    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsValid { get; }

    public string? ErrorMessage { get; }

    private WorkspaceCommitValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static WorkspaceCommitValidationResult Valid()
    {
        return _valid;
    }

    public static WorkspaceCommitValidationResult Invalid(string errorMessage)
    {
        return new WorkspaceCommitValidationResult(isValid: false, errorMessage);
    }
}
