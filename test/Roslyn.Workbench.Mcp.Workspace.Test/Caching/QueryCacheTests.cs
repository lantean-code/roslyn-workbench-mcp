using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class QueryCacheTests
{
    [Fact]
    public void GIVEN_CachedValue_WHEN_GettingSameWorkspaceAndKey_THEN_ShouldReturnValue()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var invalidationTokenSource = CreateInvalidationTokenSource();
        var target = new QueryCache(
            memoryCache,
            Options.Create(new QueryCacheOptions { SizeLimit = 10 }),
            invalidationTokenSource.Object);

        var key = new ValueKey("Key");
        var expected = new CacheValue("Value");

        target.Store("WorkspaceId", key, expected, 1);

        var found = target.TryGet<CacheValue>("WorkspaceId", new ValueKey("Key"), out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void GIVEN_CachedValue_WHEN_GettingDifferentWorkspace_THEN_ShouldReturnMiss()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var invalidationTokenSource = CreateInvalidationTokenSource();
        var target = new QueryCache(
            memoryCache,
            Options.Create(new QueryCacheOptions { SizeLimit = 10 }),
            invalidationTokenSource.Object);

        target.Store("WorkspaceId", new ValueKey("Key"), new CacheValue("Value"), 1);

        var found = target.TryGet<CacheValue>("OtherWorkspaceId", new ValueKey("Key"), out var result);

        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_CachedValue_WHEN_GettingDifferentKey_THEN_ShouldReturnMiss()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var invalidationTokenSource = CreateInvalidationTokenSource();
        var target = new QueryCache(
            memoryCache,
            Options.Create(new QueryCacheOptions { SizeLimit = 10 }),
            invalidationTokenSource.Object);

        target.Store("WorkspaceId", new ValueKey("Key"), new CacheValue("Value"), 1);

        var found = target.TryGet<CacheValue>("WorkspaceId", new ValueKey("OtherKey"), out var result);

        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void GIVEN_InvalidEntrySize_WHEN_CachingValue_THEN_ShouldNotCache(long size)
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var invalidationTokenSource = CreateInvalidationTokenSource();
        var target = new QueryCache(
            memoryCache,
            Options.Create(new QueryCacheOptions { SizeLimit = 10 }),
            invalidationTokenSource.Object);

        target.Store("WorkspaceId", new ValueKey("Key"), new CacheValue("Value"), size);

        target.TryGet<CacheValue>("WorkspaceId", new ValueKey("Key"), out _).Should().BeFalse();
        invalidationTokenSource.Verify(
            item => item.GetInvalidationToken(It.IsAny<string>()),
            Times.Never);
    }

    private static Mock<IQueryCacheInvalidationTokenSource> CreateInvalidationTokenSource()
    {
        var invalidationTokenSource = new Mock<IQueryCacheInvalidationTokenSource>();
        invalidationTokenSource
            .Setup(item => item.GetInvalidationToken(It.IsAny<string>()))
            .Returns(new CancellationChangeToken(CancellationToken.None));

        return invalidationTokenSource;
    }

    private sealed record CacheValue
    {
        public string Value { get; }

        public CacheValue(string value)
        {
            Value = value;
        }
    }

    private sealed record ValueKey
    {
        public string Value { get; }

        public ValueKey(string value)
        {
            Value = value;
        }
    }
}
