namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Coordinates concurrent read operations and exclusive lifecycle or mutation operations for one Workspace session.
/// </summary>
internal interface IWorkspaceOperationGate
{
    /// <summary>
    /// Attempts to acquire a shared lease when no exclusive operation owns the session.
    /// </summary>
    /// <returns>A shared lease, or <see langword="null"/> when acquisition would conflict.</returns>
    IWorkspaceOperationLease? TryAcquireShared();

    /// <summary>
    /// Attempts to acquire exclusive ownership when no other operation owns the session.
    /// </summary>
    /// <returns>An exclusive lease, or <see langword="null"/> when acquisition would conflict.</returns>
    IWorkspaceOperationLease? TryAcquireExclusive();
}
