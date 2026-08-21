using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Execution;

public sealed class QueryResultCacheScopeTests
{
    [Fact]
    public void GIVEN_MatchingScopes_WHEN_GettingSameKey_THEN_ShouldReuseStoredValue()
    {
        using var state = CreateState();
        var factory = CreateFactory(state);
        using var firstScope = factory.CreateScope(CreateSnapshot(), "PluginId", "ToolName");
        using var secondScope = factory.CreateScope(CreateSnapshot(), "PluginId", "ToolName");
        var factoryCalls = 0;

        var first = firstScope.GetOrCreate(
            new TestKey("Key"),
            _ =>
            {
                factoryCalls++;
                return new TestValue("Value");
            },
            CancellationToken.None);

        var second = secondScope.GetOrCreate(
            new TestKey("Key"),
            _ =>
            {
                factoryCalls++;
                return new TestValue("OtherValue");
            },
            CancellationToken.None);

        second.Should().BeSameAs(first);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_DifferentPluginToolSnapshotKeyOrValueIdentity_WHEN_GettingValues_THEN_ShouldIsolateEntries()
    {
        using var state = CreateState();
        var factory = CreateFactory(state);
        using var baseline = factory.CreateScope(CreateSnapshot(), "PluginId", "ToolName");
        using var otherPlugin = factory.CreateScope(CreateSnapshot(), "OtherPluginId", "ToolName");
        using var otherTool = factory.CreateScope(CreateSnapshot(), "PluginId", "OtherToolName");
        using var otherSnapshot = factory.CreateScope(CreateSnapshot(snapshotId: 2), "PluginId", "ToolName");
        var factoryCalls = 0;

        baseline.GetOrCreate(new TestKey("Key"), CreateValue, CancellationToken.None);
        baseline.GetOrCreate(new OtherTestKey("Key"), CreateValue, CancellationToken.None);
        baseline.GetOrCreate<TestKey, OtherTestValue>(
            new TestKey("Key"),
            _ => new OtherTestValue("Value"),
            CancellationToken.None);

        otherPlugin.GetOrCreate(new TestKey("Key"), CreateValue, CancellationToken.None);
        otherTool.GetOrCreate(new TestKey("Key"), CreateValue, CancellationToken.None);
        otherSnapshot.GetOrCreate(new TestKey("Key"), CreateValue, CancellationToken.None);

        factoryCalls.Should().Be(5);
        return;

        TestValue CreateValue(CancellationToken _)
        {
            factoryCalls++;
            return new TestValue("Value");
        }
    }

    [Fact]
    public async Task GIVEN_SynchronousAndAsynchronousCallers_WHEN_MissingTogether_THEN_ShouldCoalesceWithoutSharingCallerCancellation()
    {
        using var state = CreateState();
        var factory = CreateFactory(state);
        using var scope = factory.CreateScope(CreateSnapshot(), "PluginId", "ToolName");
        using var firstCancellationSource = new CancellationTokenSource();
        var factoryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCompletion = new TaskCompletionSource<TestValue>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        var first = scope.GetOrCreateAsync(
            new TestKey("Key"),
            async _ =>
            {
                factoryCalls++;
                factoryStarted.SetResult();
                return await factoryCompletion.Task;
            },
            firstCancellationSource.Token).AsTask();

        await factoryStarted.Task;
        var second = Task.Run(() => scope.GetOrCreate(
            new TestKey("Key"),
            _ => new TestValue("UnexpectedValue"),
            CancellationToken.None));

        await firstCancellationSource.CancelAsync();
        factoryCompletion.SetResult(new TestValue("Value"));

        await first.Invoking(static task => task).Should().ThrowAsync<OperationCanceledException>();
        var result = await second;

        result!.Value.Should().Be("Value");
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_NullOrDisposableResult_WHEN_GettingAgain_THEN_ShouldRecompute()
    {
        using var state = CreateState();
        var factory = CreateFactory(state);
        using var scope = factory.CreateScope(CreateSnapshot(), "PluginId", "ToolName");
        var nullFactoryCalls = 0;
        var disposableFactoryCalls = 0;

        scope.GetOrCreate<TestKey, string>(
            new TestKey("NullKey"),
            _ =>
            {
                nullFactoryCalls++;
                return null;
            },
            CancellationToken.None);

        scope.GetOrCreate<TestKey, string>(
            new TestKey("NullKey"),
            _ =>
            {
                nullFactoryCalls++;
                return null;
            },
            CancellationToken.None);

        scope.GetOrCreate(
            new TestKey("DisposableKey"),
            _ =>
            {
                disposableFactoryCalls++;
                return new DisposableValue();
            },
            CancellationToken.None);

        scope.GetOrCreate(
            new TestKey("DisposableKey"),
            _ =>
            {
                disposableFactoryCalls++;
                return new DisposableValue();
            },
            CancellationToken.None);

        nullFactoryCalls.Should().Be(2);
        disposableFactoryCalls.Should().Be(2);
    }

    [Fact]
    public void GIVEN_RecursiveFactory_WHEN_RequestingSameKey_THEN_ShouldFailWithoutHanging()
    {
        using var state = CreateState();
        var factory = CreateFactory(state);
        using var scope = factory.CreateScope(CreateSnapshot(), "PluginId", "ToolName");

        var action = () => scope.GetOrCreate(
            new TestKey("Key"),
            _ => scope.GetOrCreate(
                new TestKey("Key"),
                _ => new TestValue("Value"),
                CancellationToken.None),
            CancellationToken.None);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*recursively*");
    }

    [Fact]
    public void GIVEN_CompletedInvocation_WHEN_UsingRetainedScope_THEN_ShouldFailBeforeFactoryRuns()
    {
        using var state = CreateState();
        var factory = CreateFactory(state);
        var scope = factory.CreateScope(CreateSnapshot(), "PluginId", "ToolName");
        scope.Dispose();
        var factoryCalled = false;

        var action = () => scope.GetOrCreate(
            new TestKey("Key"),
            _ =>
            {
                factoryCalled = true;
                return new TestValue("Value");
            },
            CancellationToken.None);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*no longer active*");
        factoryCalled.Should().BeFalse();
    }

    private static PluginQueryCacheState CreateState()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        var cacheOptions = new PluginQueryCacheOptions
        {
            EntryLimit = 100,
            SlidingExpiration = TimeSpan.FromHours(1),
        };

        var configuredOptions = Options.Create(cacheOptions);
        var state = new PluginQueryCacheState(
            configuredOptions,
            applicationLifetime.Object);

        return state;
    }

    private static QueryResultCacheScopeFactory CreateFactory(IPluginQueryCacheState state)
    {
        var store = new PluginQueryCacheStore(state);
        var factory = new QueryResultCacheScopeFactory(store);
        return factory;
    }

    private static WorkspaceSnapshotIdentity CreateSnapshot(long snapshotId = 1)
    {
        var typedSnapshotId = WorkspaceSnapshotTestFactory.CreateId(snapshotId);
        var snapshotIdentity = new WorkspaceSnapshotIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            typedSnapshotId,
            transactionId: null);

        return snapshotIdentity;
    }

    private sealed record TestKey : IQueryResultCacheKey
    {
        public string Value { get; }

        public TestKey(string value)
        {
            Value = value;
        }
    }

    private sealed record OtherTestKey : IQueryResultCacheKey
    {
        public string Value { get; }

        public OtherTestKey(string value)
        {
            Value = value;
        }
    }

    private sealed record TestValue
    {
        public string Value { get; }

        public TestValue(string value)
        {
            Value = value;
        }
    }

    private sealed record OtherTestValue
    {
        public string Value { get; }

        public OtherTestValue(string value)
        {
            Value = value;
        }
    }

    private sealed class DisposableValue : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
