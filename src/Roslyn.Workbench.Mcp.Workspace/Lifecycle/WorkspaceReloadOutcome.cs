namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Describes a workspace after its solution and input state have been reloaded.
/// </summary>
internal sealed record WorkspaceReloadOutcome
{
    /// <summary>
    /// Gets the reloaded workspace identity.
    /// </summary>
    public required WorkspaceIdentity Workspace { get; init; }

    /// <summary>
    /// Gets the number of supported projects retained after reloading.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the number of documents retained across the supported projects.
    /// </summary>
    public int DocumentCount { get; init; }

    /// <summary>
    /// Gets diagnostics accumulated while reloading and validating the workspace.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
