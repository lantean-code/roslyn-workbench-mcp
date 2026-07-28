using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a bounded collection of applicable code actions.
/// </summary>
internal sealed record CodeActionListData
{
    /// <summary>
    /// Gets the returned actions.
    /// </summary>
    public IReadOnlyList<CodeActionListItem> Actions { get; init; } = [];

    /// <summary>
    /// Gets the number of returned actions.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether additional actions were available.
    /// </summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// Gets the complete action count when it was known without additional discovery.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; init; }
}
