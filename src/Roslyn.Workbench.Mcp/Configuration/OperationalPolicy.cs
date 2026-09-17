namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Represents the immutable policy primitives resolved from an operational profile.
/// </summary>
internal sealed record OperationalPolicy
{
    /// <summary>
    /// Gets the selected operational profile.
    /// </summary>
    public required OperationalMode Mode { get; init; }

    /// <summary>
    /// Gets the effective supported source-mutation policy.
    /// </summary>
    public required SourceMutationPolicy SourceMutation { get; init; }

    /// <summary>
    /// Gets a value indicating whether source-mutation tools and operations are enabled.
    /// </summary>
    public bool SourceMutationEnabled => SourceMutation == SourceMutationPolicy.Enabled;

    /// <summary>
    /// Gets the effective transaction commit-authorisation policy.
    /// </summary>
    public required CommitAuthorisationPolicy CommitAuthorisation { get; init; }

    /// <summary>
    /// Gets the independently configured transaction commit-validation policy.
    /// </summary>
    public required CommitValidationPolicy CommitValidation { get; init; }

    /// <summary>
    /// Gets a value indicating whether no-new-compiler-error validation is required before commit.
    /// </summary>
    public bool CompilerValidationRequired => CommitValidation == CommitValidationPolicy.NoNewCompilerErrors;
}
