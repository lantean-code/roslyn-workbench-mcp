namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents the applicable code actions at the selected location.
/// </summary>
internal sealed record CodeActionListData
{
    /// <summary>
    /// Gets the returned actions.
    /// </summary>
    public IReadOnlyList<CodeActionInfo> Actions { get; init; } = [];
}
