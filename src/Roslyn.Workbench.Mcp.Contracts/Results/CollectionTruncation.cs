using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents the reason a collection result was truncated.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CollectionTruncation>))]
public enum CollectionTruncation
{
    /// <summary>
    /// The result was truncated by the requested result limit.
    /// </summary>
    ResultLimit,

    /// <summary>
    /// The result was truncated by the response byte limit.
    /// </summary>
    ResponseByteLimit,

    /// <summary>
    /// The result was truncated by a node limit.
    /// </summary>
    NodeLimit,

    /// <summary>
    /// The result was truncated by an edge limit.
    /// </summary>
    EdgeLimit,
}
