using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal interface ICodeActionReferenceStore
{
    bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference);

    bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference);

    void Remove(Guid actionId);
}
