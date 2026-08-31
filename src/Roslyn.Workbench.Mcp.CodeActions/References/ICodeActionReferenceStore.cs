using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Stores temporary references used to rediscover Code Actions across tool calls.
/// </summary>
internal interface ICodeActionReferenceStore
{
    /// <summary>
    /// Attempts to store a replay recipe and create a temporary reference for it.
    /// </summary>
    /// <param name="recipe">The replay recipe used to reconstruct the Code Action.</param>
    /// <param name="expiresAt">The time after which the stored value is no longer valid.</param>
    /// <param name="reference">The created reference when storage succeeds.</param>
    /// <returns><see langword="true"/> when the reference was stored; otherwise, <see langword="false"/>.</returns>
    bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference);

    /// <summary>
    /// Attempts to retrieve the Code Action reference.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="reference">The matching unexpired reference, when found.</param>
    /// <returns><see langword="true"/> when an unexpired reference exists; otherwise, <see langword="false"/>.</returns>
    bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference);

    /// <summary>
    /// Determines whether the reference identifies a prepared Fix All operation.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <returns><see langword="true"/> when the reference exists and represents a prepared Fix All operation; otherwise, <see langword="false"/>.</returns>
    bool IsPreparedFixAll(Guid actionId);

    /// <summary>
    /// Removes the Code Action reference.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    void Remove(Guid actionId);
}
