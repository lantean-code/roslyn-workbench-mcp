using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class QueryCacheMemoryOptionsConfiguration : IConfigureOptions<MemoryCacheOptions>
{
    private readonly IOptions<QueryCacheOptions> _queryCacheOptions;

    public QueryCacheMemoryOptionsConfiguration(IOptions<QueryCacheOptions> queryCacheOptions)
    {
        _queryCacheOptions = queryCacheOptions;
    }

    public void Configure(MemoryCacheOptions options)
    {
        options.SizeLimit = _queryCacheOptions.Value.SizeLimit;
    }
}
