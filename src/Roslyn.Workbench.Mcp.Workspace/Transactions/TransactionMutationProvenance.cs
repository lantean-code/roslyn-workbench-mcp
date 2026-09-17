using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes one mutation revision contributing to the current reviewed transaction state.
/// </summary>
internal sealed record TransactionMutationProvenance
{
    /// <summary>
    /// Gets the one-based transaction revision.
    /// </summary>
    public required int Revision { get; init; }

    /// <summary>
    /// Gets the mutation operation recorded for the revision.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the concise mutation summary recorded for the revision.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets concise Code Action attribution when the revision originated from a Code Action.
    /// </summary>
    [Description("Code Action attribution when this revision was produced by a Code Action.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CodeActionMutationProvenance? CodeAction { get; init; }
}
