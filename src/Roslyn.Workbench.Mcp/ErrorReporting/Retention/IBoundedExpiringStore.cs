using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

internal interface IBoundedExpiringStore<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    void AddOrReplace(TKey key, TValue value);

    bool TryAdd(TKey key, TValue value);

    bool TryGet(TKey key, [NotNullWhen(true)] out TValue? value);

    BoundedExpiringStoreUpdateResult<TValue> Update(
        TKey key,
        Func<TValue, TValue> update);

    bool Remove(TKey key);
}
