using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class QueryCacheMemoryOptionsConfigurationTests
{
    [Fact]
    public void GIVEN_QueryCacheSizeLimit_WHEN_ConfiguringMemoryCache_THEN_ShouldApplySizeLimit()
    {
        var queryCacheOptions = new Mock<IOptions<QueryCacheOptions>>();
        queryCacheOptions
            .SetupGet(item => item.Value)
            .Returns(new QueryCacheOptions { SizeLimit = 42 });
        var target = new QueryCacheMemoryOptionsConfiguration(queryCacheOptions.Object);
        var options = new MemoryCacheOptions();

        target.Configure(options);

        options.SizeLimit.Should().Be(42);
    }
}
