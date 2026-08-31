using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a valid commit state or the reason validation failed.
/// </summary>
internal sealed record WorkspaceCommitValidationResult
{
    private static readonly WorkspaceCommitValidationResult _valid = new(isValid: true, errorMessage: null);

    /// <summary>
    /// Gets a value indicating whether commit state satisfies the manifest.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsValid { get; }

    /// <summary>
    /// Gets the validation failure when commit state is unsafe or inconsistent.
    /// </summary>
    public string? ErrorMessage { get; }

    private WorkspaceCommitValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a result that represents successful validation.
    /// </summary>
    /// <returns>A result that represents successful validation.</returns>
    public static WorkspaceCommitValidationResult Valid()
    {
        return _valid;
    }

    /// <summary>
    /// Creates a result that represents failed validation.
    /// </summary>
    /// <param name="errorMessage">The message that explains the failure.</param>
    /// <returns>A result that represents failed validation.</returns>
    public static WorkspaceCommitValidationResult Invalid(string errorMessage)
    {
        return new WorkspaceCommitValidationResult(isValid: false, errorMessage);
    }
}
