namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Defines the access policy applied when atomically creating a file.
/// </summary>
internal enum AtomicFileAccess
{
    /// <summary>
    /// Uses the platform's default file access.
    /// </summary>
    Default,
    /// <summary>
    /// Restricts access to the current owner where the platform supports it.
    /// </summary>
    OwnerOnly,
}
