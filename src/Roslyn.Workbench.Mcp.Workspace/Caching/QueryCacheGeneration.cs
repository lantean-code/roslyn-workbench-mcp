namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Represents one revocable generation of a cache partition.
/// </summary>
internal sealed class QueryCacheGeneration
{
    /// <summary>
    /// Gets the logical partition whose current generation this instance represents.
    /// </summary>
    public object Partition { get; }

    /// <summary>
    /// Gets the token cancelled when this generation is invalidated.
    /// </summary>
    public CancellationToken InvalidationToken { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCacheGeneration"/> class.
    /// </summary>
    /// <param name="partition">The logical cache partition.</param>
    /// <param name="invalidationToken">The token cancelled when the generation expires.</param>
    public QueryCacheGeneration(object partition, CancellationToken invalidationToken)
    {
        Partition = partition;
        InvalidationToken = invalidationToken;
    }
}
