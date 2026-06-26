using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents the kind of document change recorded in a change summary.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentChangeKind>))]
public enum DocumentChangeKind
{
    /// <summary>
    /// The document was added.
    /// </summary>
    Added,

    /// <summary>
    /// The document was modified.
    /// </summary>
    Modified,

    /// <summary>
    /// The document was deleted.
    /// </summary>
    Deleted,
}
