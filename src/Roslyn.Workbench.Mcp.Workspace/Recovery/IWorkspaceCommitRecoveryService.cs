namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Recovers or completes workspace commits left unfinished by an earlier process.
/// </summary>
internal interface IWorkspaceCommitRecoveryService
{
    /// <summary>
    /// Processes all durable recovery records that can be safely resolved.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask RecoverAsync(CancellationToken cancellationToken);
}
