namespace Roslyn.Workbench.Mcp.Workspace.Configuration;

/// <summary>
/// Represents immutable Host-owned Workspace admission and external-document policy.
/// </summary>
internal sealed class WorkspaceAuthorityOptions
{
    /// <summary>
    /// Gets or sets the roots from which the Host permits workspaces to be admitted.
    /// </summary>
    public IReadOnlyList<string> AllowedRoots { get; set; } = [];

    /// <summary>
    /// Gets or sets the policy applied to evaluated documents outside the effective Workspace root.
    /// </summary>
    public ExternalDocumentPolicy ExternalDocumentPolicy { get; set; } = ExternalDocumentPolicy.AllowReadOnly;
}
