namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes compiler-error impact for an active transaction and its affected loaded project evaluations.
/// </summary>
internal sealed record TransactionCompilerValidationOutcome
{
    /// <summary>
    /// Gets the stable comparison algorithm identifier.
    /// </summary>
    [Description("Stable diagnostic identity algorithm used for baseline comparison.")]
    public string Algorithm { get; init; } = "compiler-error-identity-v1";

    /// <summary>
    /// Gets whether every affected loaded project evaluation was completed.
    /// </summary>
    [Description("Whether every affected loaded project evaluation completed.")]
    public required bool IsComplete { get; init; }

    /// <summary>
    /// Gets whether validation completed and found no newly introduced compiler errors.
    /// </summary>
    [Description("Whether validation completed and found no newly introduced compiler errors.")]
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the structured reasons why validation could not provide complete assurance.
    /// </summary>
    [Description("Reasons why compiler-impact validation was incomplete.")]
    public IReadOnlyList<TransactionCompilerValidationIncompleteReason> IncompleteReasons { get; init; } = [];

    /// <summary>
    /// Gets the recovery action required before incomplete validation can be attempted again.
    /// </summary>
    public RequiredAction? RecoveryAction => IsComplete
        ? null
        : Results.RequiredAction.RollbackTransaction;

    /// <summary>
    /// Gets the active transaction information.
    /// </summary>
    [Description("Transaction state bound to this validation result.")]
    public required TransactionInfo Transaction { get; init; }

    /// <summary>
    /// Gets the total baseline compiler-error count in evaluated projects.
    /// </summary>
    [Description("Total baseline compiler-error count in evaluated projects.")]
    public required int BaselineErrorCount { get; init; }

    /// <summary>
    /// Gets the total staged compiler-error count in evaluated projects.
    /// </summary>
    [Description("Total staged compiler-error count in evaluated projects.")]
    public required int StagedErrorCount { get; init; }

    /// <summary>
    /// Gets the number of staged compiler errors not present in the baseline multiset.
    /// </summary>
    [Description("Number of staged compiler errors not present in the baseline multiset.")]
    public required int IntroducedErrorCount { get; init; }

    /// <summary>
    /// Gets the elapsed validation time in milliseconds.
    /// </summary>
    [Description("Elapsed compiler validation time in milliseconds.")]
    public required long DurationMilliseconds { get; init; }

    /// <summary>
    /// Gets per-project validation summaries in stable order.
    /// </summary>
    [Description("Affected loaded project evaluations and their compiler-error counts.")]
    public IReadOnlyList<TransactionCompilerProjectValidation> Projects { get; init; } = [];

    /// <summary>
    /// Gets a bounded projection of newly introduced compiler errors.
    /// </summary>
    [Description("Bounded projection of newly introduced compiler errors.")]
    public IReadOnlyList<DiagnosticInfo> IntroducedDiagnostics { get; init; } = [];

    /// <summary>
    /// Gets limitations or failures that constrain the validation assurance.
    /// </summary>
    [Description("Load, compilation and target-framework limitations relevant to the result.")]
    public IReadOnlyList<string> Limitations { get; init; } = [];
}
