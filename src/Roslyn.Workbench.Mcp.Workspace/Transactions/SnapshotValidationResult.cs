using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a satisfied snapshot precondition or a structured mismatch error.
/// </summary>
internal sealed record SnapshotValidationResult
{
    private static readonly SnapshotValidationResult _valid = new(isValid: true, error: null);

    /// <summary>
    /// Gets a value indicating whether the snapshot precondition is satisfied.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid { get; }

    /// <summary>
    /// Gets the structured mismatch error when validation fails.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    private SnapshotValidationResult(bool isValid, WorkspaceOperationError? error)
    {
        IsValid = isValid;
        Error = error;
    }

    /// <summary>
    /// Creates a result that represents successful validation.
    /// </summary>
    /// <returns>A result that represents successful validation.</returns>
    public static SnapshotValidationResult Valid()
    {
        return _valid;
    }

    /// <summary>
    /// Creates a result that represents failed validation.
    /// </summary>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result that represents failed validation.</returns>
    public static SnapshotValidationResult Invalid(WorkspaceOperationError error)
    {
        return new SnapshotValidationResult(isValid: false, error);
    }
}
