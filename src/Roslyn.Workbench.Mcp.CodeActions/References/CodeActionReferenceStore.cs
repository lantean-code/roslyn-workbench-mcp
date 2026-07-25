using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed class CodeActionReferenceStore : ICodeActionReferenceStore
{
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;

    public CodeActionReferenceStore(IMemoryCache cache, TimeProvider timeProvider)
    {
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        var actionId = Guid.NewGuid();
        var candidate = new CodeActionReference(actionId, recipe, expiresAt);
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expiresAt)
            .SetSize(CalculateSize(recipe));

        var key = new CodeActionReferenceCacheKey(actionId);
        _cache.Set(key, candidate, options);
        return TryGet(actionId, out reference);
    }

    public bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        var key = new CodeActionReferenceCacheKey(actionId);
        if (!_cache.TryGetValue(key, out reference) || reference is null)
        {
            return false;
        }

        if (reference.ExpiresAt >= _timeProvider.GetUtcNow())
        {
            return true;
        }

        _cache.Remove(key);
        reference = null;
        return false;
    }

    public void Remove(Guid actionId)
    {
        _cache.Remove(new CodeActionReferenceCacheKey(actionId));
    }

    private static long CalculateSize(CodeActionReplayRecipe recipe)
    {
        var size = 1L
            + recipe.ProviderId.Length
            + recipe.Title.Length
            + (recipe.EquivalenceKey?.Length ?? 0)
            + (recipe.WorkspaceId?.Length ?? 0)
            + recipe.DocumentPath.Length
            + recipe.ProjectId.Length
            + recipe.ActionPath.Count;

        foreach (var diagnosticId in recipe.DiagnosticIds)
        {
            size += diagnosticId.Length;
        }

        return size;
    }

    private sealed record CodeActionReferenceCacheKey
    {
        private Guid ActionId { get; }

        public CodeActionReferenceCacheKey(Guid actionId)
        {
            ActionId = actionId;
        }
    }
}
