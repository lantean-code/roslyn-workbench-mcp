namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Binds a component-specific cache scope to a revocable partition generation.
/// </summary>
internal sealed class QueryCacheScopeIdentity
{
    /// <summary>
    /// Gets the partition generation that controls entry validity.
    /// </summary>
    public QueryCacheGeneration Generation { get; }

    /// <summary>
    /// Gets the value that isolates entries belonging to a component or tool scope.
    /// </summary>
    public object Scope { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCacheScopeIdentity"/> class.
    /// </summary>
    /// <param name="generation">The partition generation that controls validity.</param>
    /// <param name="scope">The component-specific scope value.</param>
    public QueryCacheScopeIdentity(QueryCacheGeneration generation, object scope)
    {
        Generation = generation;
        Scope = scope;
    }
}
