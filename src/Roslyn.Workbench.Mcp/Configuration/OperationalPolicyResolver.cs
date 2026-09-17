namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Resolves named operational profiles into explicit immutable policy primitives.
/// </summary>
internal static class OperationalPolicyResolver
{
    /// <summary>
    /// Resolves the supplied operational mode into its effective policy.
    /// </summary>
    /// <param name="mode">The startup-selected operational mode.</param>
    /// <returns>The effective immutable operational policy.</returns>
    public static OperationalPolicy Resolve(OperationalMode mode)
    {
        return Resolve(mode, CommitValidationPolicy.None);
    }

    /// <summary>
    /// Resolves the supplied operational mode and independent commit-validation setting into their effective policy.
    /// </summary>
    /// <param name="mode">The startup-selected operational mode.</param>
    /// <param name="commitValidation">The independently configured transaction commit-validation policy.</param>
    /// <returns>The effective immutable operational policy.</returns>
    public static OperationalPolicy Resolve(
        OperationalMode mode,
        CommitValidationPolicy commitValidation)
    {
        return mode switch
        {
            OperationalMode.InspectionOnly => Create(mode, SourceMutationPolicy.Disabled, CommitAuthorisationPolicy.None, commitValidation),
            OperationalMode.Transactional => Create(mode, SourceMutationPolicy.Enabled, CommitAuthorisationPolicy.Confirmation, commitValidation),
            OperationalMode.ApprovalRequired => Create(mode, SourceMutationPolicy.Enabled, CommitAuthorisationPolicy.ReceiptApproval, commitValidation),
            OperationalMode.AutonomousTrusted => Create(mode, SourceMutationPolicy.Enabled, CommitAuthorisationPolicy.None, commitValidation),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static OperationalPolicy Create(
        OperationalMode mode,
        SourceMutationPolicy sourceMutation,
        CommitAuthorisationPolicy commitAuthorisation,
        CommitValidationPolicy commitValidation)
    {
        return new OperationalPolicy
        {
            Mode = mode,
            SourceMutation = sourceMutation,
            CommitAuthorisation = commitAuthorisation,
            CommitValidation = commitValidation,
        };
    }
}
