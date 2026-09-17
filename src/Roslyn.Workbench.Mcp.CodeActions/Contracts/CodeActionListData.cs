using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a bounded collection of applicable code actions.
/// </summary>
internal sealed record CodeActionListData
{
    /// <summary>
    /// The returned actions.
    /// </summary>
    [Description("The returned actions.")]
    public BoundedCollection<CodeActionListItem> Actions { get; init; }
        = BoundedCollection.Empty<CodeActionListItem>();

    /// <summary>
    /// Gets provider identities keyed by response-local identifiers when provenance was requested.
    /// </summary>
    [Description("Provider identities keyed by response-local references used by the returned actions.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, MutationProviderIdentity>? Providers { get; init; }
}
