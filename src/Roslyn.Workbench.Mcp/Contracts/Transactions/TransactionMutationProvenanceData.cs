using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents one mutation revision in an agent-facing transaction response.
/// </summary>
internal sealed record TransactionMutationProvenanceData
{
    /// <summary>
    /// Gets the one-based transaction revision.
    /// </summary>
    [Description("One-based transaction revision.")]
    public required int Revision { get; init; }

    /// <summary>
    /// Gets the mutation operation recorded for the revision.
    /// </summary>
    [Description("Mutation operation recorded for the revision.")]
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the concise mutation summary recorded for the revision.
    /// </summary>
    [Description("Concise mutation summary recorded for the revision.")]
    public required string Summary { get; init; }

    /// <summary>
    /// Gets concise Code Action attribution when the revision originated from a Code Action.
    /// </summary>
    [Description("Code Action attribution when this revision was produced by a Code Action.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CodeActionMutationProvenanceData? CodeAction { get; init; }
}
