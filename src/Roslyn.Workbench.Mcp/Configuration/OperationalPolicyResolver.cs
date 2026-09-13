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
        return mode switch
        {
            OperationalMode.InspectionOnly => Create(mode, SourceMutationPolicy.Disabled, CommitAuthorisationPolicy.None),
            OperationalMode.Transactional => Create(mode, SourceMutationPolicy.Enabled, CommitAuthorisationPolicy.Confirmation),
            OperationalMode.ApprovalRequired => Create(mode, SourceMutationPolicy.Enabled, CommitAuthorisationPolicy.ReceiptApproval),
            OperationalMode.AutonomousTrusted => Create(mode, SourceMutationPolicy.Enabled, CommitAuthorisationPolicy.None),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static OperationalPolicy Create(
        OperationalMode mode,
        SourceMutationPolicy sourceMutation,
        CommitAuthorisationPolicy commitAuthorisation)
    {
        return new OperationalPolicy
        {
            Mode = mode,
            SourceMutation = sourceMutation,
            CommitAuthorisation = commitAuthorisation,
        };
    }
}
