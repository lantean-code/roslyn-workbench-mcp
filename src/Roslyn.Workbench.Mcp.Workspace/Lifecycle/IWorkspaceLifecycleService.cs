namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Executes the server-owned workspace lifecycle operations.
/// </summary>
internal interface IWorkspaceLifecycleService
{
    /// <summary>
    /// Opens the requested workspace using automatic root discovery.
    /// </summary>
    /// <param name="path">The absolute solution or project path.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The open result.</returns>
    ValueTask<WorkspaceOperationResult<WorkspaceOpenOutcome>> OpenAsync(
        string path,
        string? alias,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens the requested workspace.
    /// </summary>
    /// <param name="path">The absolute solution or project path.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="workspaceRoot">The optional repository or workspace root.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The open result.</returns>
    ValueTask<WorkspaceOperationResult<WorkspaceOpenOutcome>> OpenAsync(
        string path,
        string? alias,
        string? workspaceRoot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists the currently loaded workspaces.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list result.</returns>
    ValueTask<WorkspaceOperationResult<WorkspaceListOutcome>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Closes the selected workspace.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The close result.</returns>
    ValueTask<WorkspaceOperationResult<WorkspaceCloseOutcome>> CloseAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the selected workspace status.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="detail">The requested detail level.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workspace status.</returns>
    ValueTask<WorkspaceOperationResult<WorkspaceStatusOutcome>> GetStatusAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        StatusDetailLevel detail,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reloads the selected workspace.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The reload result.</returns>
    ValueTask<WorkspaceOperationResult<WorkspaceReloadOutcome>> ReloadAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken);

    /// <summary>
    /// Releases every open workspace during application shutdown.
    /// </summary>
    /// <returns>A task that represents the asynchronous shutdown operation.</returns>
    ValueTask ShutdownAsync();
}
