namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Requests protocol-neutral user choices through the connected client.
/// </summary>
internal interface IUserInteractionService
{
    /// <summary>
    /// Requests one titled single-select decision from the connected client.
    /// </summary>
    /// <param name="request">The protocol-neutral interaction request.</param>
    /// <param name="cancellationToken">The token used to cancel the interaction.</param>
    /// <returns>A task containing the normalised interaction outcome.</returns>
    ValueTask<UserInteractionResult> RequestAsync(UserInteractionRequest request, CancellationToken cancellationToken);
}
