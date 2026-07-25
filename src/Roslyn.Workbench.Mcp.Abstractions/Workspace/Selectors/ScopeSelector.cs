namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents the scope selector for scoped requests.
/// </summary>
public sealed record ScopeSelector
{
    /// <summary>
    /// Gets the scope kind.
    /// </summary>
    public ScopeKind Kind { get; init; }

    /// <summary>
    /// Gets the single project selector when the scope kind is <see cref="ScopeKind.Project"/>.
    /// </summary>
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets the single document selector when the scope kind is <see cref="ScopeKind.Document"/>.
    /// </summary>
    public DocumentSelector? Document { get; init; }

    /// <summary>
    /// Gets the selected project set when the scope kind is <see cref="ScopeKind.Projects"/>.
    /// </summary>
    public IReadOnlyList<ProjectSelector>? Projects { get; init; }
}
