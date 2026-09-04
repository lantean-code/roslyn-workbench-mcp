using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class QueryCacheEntryIdentityTests
{
    [Fact]
    public void GIVEN_EquivalentIdentity_WHEN_Comparing_THEN_ShouldBeEqualWithMatchingHashCode()
    {
        var generation = new QueryCacheGeneration("Partition", CancellationToken.None);
        var first = CreateIdentity(generation, "Scope", typeof(TestKey), typeof(TestValue), new TestKey("Key"));
        var second = CreateIdentity(generation, "Scope", typeof(TestKey), typeof(TestValue), new TestKey("Key"));

        var typedResult = first.Equals(second);
        var objectResult = first.Equals((object)second);

        typedResult.Should().BeTrue();
        objectResult.Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void GIVEN_NullIdentity_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var target = CreateIdentity();

        var typedResult = target.Equals(other: null);
        var objectResult = object.Equals(target, null);

        typedResult.Should().BeFalse();
        objectResult.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DifferentObjectType_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var target = CreateIdentity();

        var result = target.Equals(new object());

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DifferentGeneration_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var target = CreateIdentity();
        var otherGeneration = new QueryCacheGeneration("Partition", CancellationToken.None);
        var other = CreateIdentity(otherGeneration);

        target.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DifferentScope_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var generation = new QueryCacheGeneration("Partition", CancellationToken.None);
        var target = CreateIdentity(generation, "FirstScope");
        var other = CreateIdentity(generation, "SecondScope");

        target.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DifferentKeyType_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var generation = new QueryCacheGeneration("Partition", CancellationToken.None);
        var target = CreateIdentity(generation);
        var other = CreateIdentity(generation, keyType: typeof(string));

        target.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DifferentValueType_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var generation = new QueryCacheGeneration("Partition", CancellationToken.None);
        var target = CreateIdentity(generation);
        var other = CreateIdentity(generation, valueType: typeof(string));

        target.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DifferentKey_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var generation = new QueryCacheGeneration("Partition", CancellationToken.None);
        var target = CreateIdentity(generation);
        var other = CreateIdentity(generation, key: new TestKey("OtherKey"));

        target.Equals(other).Should().BeFalse();
    }

    private static QueryCacheEntryIdentity CreateIdentity(
        QueryCacheGeneration? generation = null,
        object? scope = null,
        Type? keyType = null,
        Type? valueType = null,
        object? key = null)
    {
        generation ??= new QueryCacheGeneration("Partition", CancellationToken.None);
        var scopeIdentity = new QueryCacheScopeIdentity(generation, scope ?? "Scope");
        return new QueryCacheEntryIdentity(
            scopeIdentity,
            keyType ?? typeof(TestKey),
            valueType ?? typeof(TestValue),
            key ?? new TestKey("Key"));
    }

    private sealed record TestKey
    {
        public string Value { get; }

        public TestKey(string value)
        {
            Value = value;
        }
    }

    private static class TestValue
    {
    }
}
