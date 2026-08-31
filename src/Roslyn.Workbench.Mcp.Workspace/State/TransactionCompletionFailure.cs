namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Describes an invariant failure where process-wide transaction ownership changed before completion.
/// </summary>
internal sealed record TransactionCompletionFailure
{
    /// <summary>
    /// Gets the Workspace expected to own the transaction slot.
    /// </summary>
    public Guid ExpectedOwnerWorkspaceId { get; }

    /// <summary>
    /// Gets the owner observed at completion, or <see langword="null"/> when no owner was recorded.
    /// </summary>
    public Guid? ObservedOwnerWorkspaceId { get; }

    /// <summary>
    /// Gets the actionable invariant-failure message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCompletionFailure"/> class.
    /// </summary>
    /// <param name="expectedOwnerWorkspaceId">The Workspace expected to own the transaction.</param>
    /// <param name="observedOwnerWorkspaceId">The owner observed during completion.</param>
    public TransactionCompletionFailure(Guid expectedOwnerWorkspaceId, Guid? observedOwnerWorkspaceId)
    {
        ExpectedOwnerWorkspaceId = expectedOwnerWorkspaceId;
        ObservedOwnerWorkspaceId = observedOwnerWorkspaceId;
        Message = CreateMessage(expectedOwnerWorkspaceId, observedOwnerWorkspaceId);
    }

    private static string CreateMessage(Guid expectedOwnerWorkspaceId, Guid? observedOwnerWorkspaceId)
    {
        if (observedOwnerWorkspaceId is not { } ownerWorkspaceId)
        {
            return $"Transaction ownership for workspace '{expectedOwnerWorkspaceId}' was lost before completion, and no active owner is recorded. Restart the server before continuing.";
        }

        return $"Transaction ownership for workspace '{expectedOwnerWorkspaceId}' changed to workspace '{ownerWorkspaceId}' before completion. Restart the server before continuing.";
    }
}
