using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Records concise runtime attribution and internal audit metadata for a Code Action mutation.
/// </summary>
internal sealed record CodeActionMutationProvenance
{
    /// <summary>
    /// Gets the Code Action family that produced the mutation.
    /// </summary>
    [Description("Code Action family that produced the mutation.")]
    public required CodeActionMutationKind Kind { get; init; }

    /// <summary>
    /// Gets the originating Code Fix or refactoring provider.
    /// </summary>
    [Description("Runtime provider that originated the Code Action.")]
    public required MutationProviderIdentity Provider { get; init; }

    /// <summary>
    /// Gets the Fix All provider that assembled the operation, when applicable.
    /// </summary>
    [Description("Runtime Fix All provider when it differs from ordinary Code Action execution.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MutationProviderIdentity? FixAllProvider { get; init; }

    /// <summary>
    /// Gets the diagnostic identifiers retained for structured local logging.
    /// </summary>
    internal IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    /// <summary>
    /// Gets the provider equivalence key retained for structured local logging.
    /// </summary>
    internal string? EquivalenceKey { get; init; }

    /// <summary>
    /// Gets the Fix All scope retained for structured local logging.
    /// </summary>
    internal string? FixAllScope { get; init; }
}
