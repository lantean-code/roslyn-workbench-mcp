namespace Roslyn.Workbench.Mcp.Workspace.Configuration;

/// <summary>
/// Defines how evaluated documents outside the effective Workspace root are handled.
/// </summary>
internal enum ExternalDocumentPolicy
{
    /// <summary>
    /// Allows external documents to participate in inspection after their loaded text is certified against disk.
    /// </summary>
    AllowReadOnly,

    /// <summary>
    /// Rejects a Workspace containing any evaluated document outside its effective root.
    /// </summary>
    RejectWorkspace,
}
