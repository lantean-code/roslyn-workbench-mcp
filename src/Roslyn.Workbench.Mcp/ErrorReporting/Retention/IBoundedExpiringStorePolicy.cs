using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

internal interface IBoundedExpiringStorePolicy<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    int Capacity { get; }

    DateTimeOffset GetExpiration(TValue value);

    bool TrySelectEvictionKey(
        IReadOnlyDictionary<TKey, TValue> entries,
        [MaybeNullWhen(false)] out TKey key);
}
