namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Associates a temporary action identifier with the information needed to rediscover a Code Action.
/// </summary>
internal sealed record CodeActionReference
{
    /// <summary>
    /// Gets the temporary identifier exposed to callers.
    /// </summary>
    public Guid ActionId { get; }

    /// <summary>
    /// Gets the recipe used to rediscover the action.
    /// </summary>
    public CodeActionReplayRecipe Recipe { get; }

    /// <summary>
    /// Gets the time after which the reference is no longer valid.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionReference"/> class.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="recipe">The replay recipe used to reconstruct the Code Action.</param>
    /// <param name="expiresAt">The time after which the stored value is no longer valid.</param>
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
