using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.CodeActions;

/// <summary>
/// Represents a bounded list of applicable code actions.
/// </summary>
public sealed record CodeActionListData
{
    /// <summary>
    /// Gets the returned actions.
    /// </summary>
    public IReadOnlyList<CodeActionInfo> Actions { get; init; } = [];

    /// <summary>
    /// Gets the number of returned actions.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more actions were available.
    /// </summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// Gets the optional truncation reasons.
    /// </summary>
    public IReadOnlyList<CollectionTruncation>? TruncationReasons { get; init; }
}
