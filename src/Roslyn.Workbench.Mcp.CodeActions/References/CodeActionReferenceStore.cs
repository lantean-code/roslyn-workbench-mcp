using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Provides the application-facing store for temporary Code Action references.
/// </summary>
internal sealed class CodeActionReferenceStore : ICodeActionReferenceStore
{
    private readonly ICodeActionReferenceState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionReferenceStore"/> class.
    /// </summary>
    /// <param name="state">The cache state that backs the store.</param>
    public CodeActionReferenceStore(ICodeActionReferenceState state)
    {
        _state = state;
    }

    /// <summary>
    /// Attempts to store a replay recipe and create a temporary reference for it.
    /// </summary>
    /// <param name="recipe">The replay recipe used to reconstruct the Code Action.</param>
    /// <param name="expiresAt">The time after which the stored value is no longer valid.</param>
    /// <param name="reference">The created reference when storage succeeds.</param>
    /// <returns><see langword="true"/> when the reference was stored; otherwise, <see langword="false"/>.</returns>
    public bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        return _state.TryCreate(recipe, expiresAt, out reference);
    }

    /// <summary>
    /// Attempts to retrieve the Code Action reference.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="reference">The matching unexpired reference, when found.</param>
    /// <returns><see langword="true"/> when an unexpired reference exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        return _state.TryGet(actionId, out reference);
    }

    /// <summary>
    /// Determines whether the reference identifies a prepared Fix All operation.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <returns><see langword="true"/> when the reference exists and represents a prepared Fix All operation; otherwise, <see langword="false"/>.</returns>
    public bool IsPreparedFixAll(Guid actionId)
    {
        return _state.IsPreparedFixAll(actionId);
    }

    /// <summary>
    /// Removes the Code Action reference.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    public void Remove(Guid actionId)
    {
        _state.Remove(actionId);
    }
}
