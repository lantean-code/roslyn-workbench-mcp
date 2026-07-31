using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed class CodeActionReferenceStore : ICodeActionReferenceStore
{
    private readonly ICodeActionReferenceState _state;

    public CodeActionReferenceStore(ICodeActionReferenceState state)
    {
        _state = state;
    }

    public bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        return _state.TryCreate(recipe, expiresAt, out reference);
    }

    public bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        return _state.TryGet(actionId, out reference);
    }

    public bool IsPreparedFixAll(Guid actionId)
    {
        return _state.IsPreparedFixAll(actionId);
    }

    public void Remove(Guid actionId)
    {
        _state.Remove(actionId);
    }
}
