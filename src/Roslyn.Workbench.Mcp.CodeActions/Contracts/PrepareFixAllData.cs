using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes the bounded impact of a prepared Fix All operation.
/// </summary>
internal sealed record PrepareFixAllData
{
    /// <summary>
    /// The opaque prepared action reference.
    /// </summary>
    [Description("The opaque prepared action reference.")]
    public required Guid ActionId { get; init; }

    /// <summary>
    /// The accepted Fix All scope.
    /// </summary>
    [Description("The accepted Fix All scope.")]
    public required CodeActionFixAllScope Scope { get; init; }

    /// <summary>
    /// The complete affected diagnostic count when it is authoritatively available.
    /// </summary>
    [Description("The complete affected diagnostic count when it is authoritatively available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AffectedDiagnosticCount { get; init; }

    /// <summary>
    /// The bounded affected source document identities and complete changed-document count.
    /// </summary>
    [Description("The bounded affected source document identities and complete changed-document count.")]
    public BoundedCollection<DocumentReference> AffectedDocuments { get; init; }
        = BoundedCollection.Empty<DocumentReference>();
}
