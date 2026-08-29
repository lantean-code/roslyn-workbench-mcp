using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a bounded collection of applicable code actions.
/// </summary>
internal sealed record CodeActionListData
{
    /// <summary>
    /// Gets the returned actions.
    /// </summary>
    [Description("The returned actions.")]
    public BoundedCollection<CodeActionListItem> Actions { get; init; }
        = BoundedCollection.Empty<CodeActionListItem>();
}
