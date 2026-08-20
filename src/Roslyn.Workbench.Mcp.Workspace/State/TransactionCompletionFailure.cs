namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed record TransactionCompletionFailure
{
    public Guid ExpectedOwnerWorkspaceId { get; }

    public Guid? ObservedOwnerWorkspaceId { get; }

    public string Message { get; }

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
