using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents the scope selector for scoped requests.
/// </summary>
public sealed record ScopeSelector
{
    /// <summary>
    /// Gets the scope kind.
    /// </summary>
    [Description("How the request is scoped: Solution, Project, Document, or Projects.")]
    [DefaultValue(ScopeKind.Solution)]
    public ScopeKind Kind { get; init; }

    /// <summary>
    /// Gets the single project selector when the scope kind is <see cref="ScopeKind.Project"/>.
    /// </summary>
    [Description("Project to use when kind is Project; omit for other kinds.")]
    [RequiredWhen(nameof(Kind), ScopeKind.Project)]
    [ProhibitedUnless(nameof(Kind), ScopeKind.Project)]
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets the single document selector when the scope kind is <see cref="ScopeKind.Document"/>.
    /// </summary>
    [Description("Document to use when kind is Document; omit for other kinds.")]
    [RequiredWhen(nameof(Kind), ScopeKind.Document)]
    [ProhibitedUnless(nameof(Kind), ScopeKind.Document)]
    public DocumentSelector? Document { get; init; }

    /// <summary>
    /// Gets the selected project set when the scope kind is <see cref="ScopeKind.Projects"/>.
    /// </summary>
    [Description("Projects to use when kind is Projects; omit for other kinds.")]
    [RequiredWhen(nameof(Kind), ScopeKind.Projects)]
    [ProhibitedUnless(nameof(Kind), ScopeKind.Projects)]
    public IReadOnlyList<ProjectSelector>? Projects { get; init; }
}
