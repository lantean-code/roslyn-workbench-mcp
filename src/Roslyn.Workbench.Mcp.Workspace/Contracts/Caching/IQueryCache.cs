using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Caching;

/// <summary>
/// Provides bounded storage for reusable query results owned by the current Host process.
/// </summary>
/// <remarks>
/// Cache keys for snapshot-dependent values must include the immutable solution snapshot and all semantic inputs.
/// Callers must store only successfully completed results and must not store failed or cancelled operations.
/// </remarks>
public interface IQueryCache
{
    /// <summary>
    /// Attempts to retrieve a cached value for a workspace and operation key.
    /// </summary>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="workspaceId">The stable workspace identifier.</param>
    /// <param name="key">The operation-specific cache key.</param>
    /// <param name="value">The cached value when found.</param>
    /// <returns><see langword="true" /> when the cache contains a matching value; otherwise, <see langword="false" />.</returns>
    bool TryGet<TValue>(string workspaceId, object key, [NotNullWhen(true)] out TValue? value)
        where TValue : class;

    /// <summary>
    /// Stores a successfully completed query result when its size is accepted by the bounded cache.
    /// </summary>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="workspaceId">The stable workspace identifier.</param>
    /// <param name="key">The operation-specific cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="size">The operation-defined relative size used by the cache limit.</param>
    void Store<TValue>(string workspaceId, object key, TValue value, long size)
        where TValue : class;
}
