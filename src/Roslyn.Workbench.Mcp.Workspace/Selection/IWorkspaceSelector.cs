namespace Roslyn.Workbench.Mcp.Workspace.Selection;

/// <summary>
/// Selects one loaded workspace session from an immutable host snapshot.
/// </summary>
internal interface IWorkspaceSelector
{
    /// <summary>
    /// Selects a workspace by identifier, alias, loaded path, or unambiguous default.
    /// </summary>
    /// <param name="hostSnapshot">The immutable host catalogue snapshot used for tool selection.</param>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <returns>The selected session or a structured selection error.</returns>
    WorkspaceSelectionResult Select(WorkspaceHostSnapshot hostSnapshot, WorkspaceSelector? selector);
}
