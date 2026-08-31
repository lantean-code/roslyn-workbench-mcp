using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Represents either a selected Workspace with an operation lease or a structured acquisition failure.
/// </summary>
internal sealed class WorkspaceSessionAcquisition
{
    private WorkspaceSessionAcquisition(
        WorkspaceSelection? selection,
        WorkspaceSessionSnapshot? session,
        WorkspaceSessionSnapshot? contextSession,
        IWorkspaceOperationLease? lease,
        WorkspaceOperationError? error)
    {
        Selection = selection;
        Session = session;
        ContextSession = contextSession;
        Lease = lease;
        Error = error;
    }

    /// <summary>
    /// Gets the best available session context for enriching a rejection.
    /// </summary>
    public WorkspaceSessionSnapshot? ContextSession { get; }

    /// <summary>
    /// Gets the acquisition error when the operation was rejected.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    /// <summary>
    /// Gets the acquired lease, including a context-only lease retained by a rejection when applicable.
    /// </summary>
    public IWorkspaceOperationLease? Lease { get; }

    /// <summary>
    /// Gets the successful Workspace selection.
    /// </summary>
    public WorkspaceSelection? Selection { get; }

    /// <summary>
    /// Gets the successfully acquired session snapshot.
    /// </summary>
    public WorkspaceSessionSnapshot? Session { get; }

    /// <summary>
    /// Gets whether acquisition was rejected with an error.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Lease))]
    [MemberNotNullWhen(false, nameof(Selection))]
    [MemberNotNullWhen(false, nameof(Session))]
    public bool HasError => Error is not null;

    /// <summary>
    /// Creates a successful acquisition with ownership of the supplied lease.
    /// </summary>
    /// <param name="selection">The resolved Workspace selection.</param>
    /// <param name="session">The acquired immutable session snapshot.</param>
    /// <param name="lease">The operation lease transferred to the result.</param>
    /// <returns>A successful acquisition.</returns>
    public static WorkspaceSessionAcquisition Acquired(
        WorkspaceSelection selection,
        WorkspaceSessionSnapshot session,
        IWorkspaceOperationLease lease)
    {
        return new WorkspaceSessionAcquisition(
            selection,
            session,
            session,
            lease,
            error: null);
    }

    /// <summary>
    /// Creates a rejected acquisition with optional session context and lease ownership.
    /// </summary>
    /// <param name="error">The structured rejection.</param>
    /// <param name="contextSession">The optional session used to enrich the failure.</param>
    /// <param name="lease">An optional lease transferred to the result for caller disposal.</param>
    /// <returns>A rejected acquisition.</returns>
    public static WorkspaceSessionAcquisition Rejected(
        WorkspaceOperationError error,
        WorkspaceSessionSnapshot? contextSession = null,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceSessionAcquisition(
            selection: null,
            session: null,
            contextSession,
            lease,
            error);
    }
}
