namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Describes a workspace after it has been loaded, validated, and registered.
/// </summary>
internal sealed record WorkspaceOpenOutcome
{
    /// <summary>
    /// Gets the registered workspace identity.
    /// </summary>
    public required WorkspaceIdentity Workspace { get; init; }

    /// <summary>
    /// Gets the number of supported projects retained in the workspace.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the number of documents retained across the supported projects.
    /// </summary>
    public int DocumentCount { get; init; }

    /// <summary>
    /// Gets diagnostics accumulated while loading, validating, and registering the workspace.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
