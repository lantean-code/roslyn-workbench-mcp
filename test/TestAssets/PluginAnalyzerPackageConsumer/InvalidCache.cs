using Roslyn.Workbench.Mcp.Plugins;

public sealed class MutableKey : IQueryResultCacheKey
{
    public string Value { get; set; } = string.Empty;
}

public static class CacheConsumer
{
    public static System.IO.MemoryStream? Read(IQueryResultCache cache)
    {
        return cache.GetOrCreate(
            new MutableKey(),
            _ => new System.IO.MemoryStream(),
            default);
    }
}
