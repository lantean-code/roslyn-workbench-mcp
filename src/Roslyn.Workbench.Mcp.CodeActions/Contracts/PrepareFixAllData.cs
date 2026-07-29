using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes the bounded impact of a prepared Fix All operation.
/// </summary>
internal sealed record PrepareFixAllData
{
    /// <summary>
    /// Gets the opaque prepared action reference.
    /// </summary>
    public required Guid ActionId { get; init; }

    /// <summary>
    /// Gets the accepted Fix All scope.
    /// </summary>
    public required CodeActionFixAllScope Scope { get; init; }

    /// <summary>
    /// Gets the complete affected diagnostic count when it is authoritatively available.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AffectedDiagnosticCount { get; init; }

    /// <summary>
    /// Gets the bounded affected source document identities and complete changed-document count.
    /// </summary>
    public BoundedCollection<DocumentReference> AffectedDocuments { get; init; }
        = BoundedCollection.Empty<DocumentReference>();
}
