using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents concise Code Action attribution in a transaction response.
/// </summary>
internal sealed record CodeActionMutationProvenanceData
{
    /// <summary>
    /// Gets the Code Action family that produced the mutation.
    /// </summary>
    [Description("Code Action family that produced the mutation.")]
    public required CodeActionMutationKind Kind { get; init; }

    /// <summary>
    /// Gets the response-local reference to the originating provider.
    /// </summary>
    [Description("Response-local reference to the provider that originated the Code Action.")]
    public required string ProviderId { get; init; }

    /// <summary>
    /// Gets the response-local reference to the Fix All provider, when applicable.
    /// </summary>
    [Description("Response-local reference to the Fix All provider when one assembled the operation.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FixAllProviderId { get; init; }
}
