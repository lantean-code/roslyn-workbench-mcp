namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed record CodeActionReference
{
    public Guid ActionId { get; }

    public CodeActionReplayRecipe Recipe { get; }

    public DateTimeOffset ExpiresAt { get; }

    public CodeActionReference(
        Guid actionId,
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt)
    {
        ActionId = actionId;
        Recipe = recipe;
        ExpiresAt = expiresAt;
    }
}
