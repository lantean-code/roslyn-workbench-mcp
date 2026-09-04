using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class PluginQueryCacheStateTests : IDisposable
{
    private readonly PluginQueryCacheState _target;

    public PluginQueryCacheStateTests()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        var options = Options.Create(new PluginQueryCacheOptions
        {
            EntryLimit = 100,
            SlidingExpiration = TimeSpan.FromHours(1),
        });

        _target = new PluginQueryCacheState(options, applicationLifetime.Object);
    }

    [Fact]
    public void GIVEN_AdmissibleValue_WHEN_GettingSynchronouslyTwice_THEN_ShouldReuseCachedValue()
    {
        var scope = CreateScope(CreateSnapshotIdentity());
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        var factoryCalls = 0;

        var first = _target.GetOrCreate(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return expected;
            },
            CancellationToken.None);

        var second = _target.GetOrCreate(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return new TestValue("Unexpected");
            },
            CancellationToken.None);

        first.Should().BeSameAs(expected);
        second.Should().BeSameAs(expected);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_AdmissibleValue_WHEN_GettingAsynchronouslyTwice_THEN_ShouldReuseCachedValue()
    {
        var scope = CreateScope(CreateSnapshotIdentity());
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        var factoryCalls = 0;

        var first = await _target.GetOrCreateAsync(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult<TestValue?>(expected);
            },
            CancellationToken.None);

        var second = await _target.GetOrCreateAsync(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult<TestValue?>(new TestValue("Unexpected"));
            },
            CancellationToken.None);

        first.Should().BeSameAs(expected);
        second.Should().BeSameAs(expected);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_DisposableValue_WHEN_GettingTwice_THEN_ShouldNotCacheValue()
    {
        var scope = CreateScope(CreateSnapshotIdentity());
        var key = new TestKey("Key");
        var factoryCalls = 0;

        DisposableTestValue? CreateValue(CancellationToken _)
        {
            factoryCalls++;
            return new DisposableTestValue();
        }

        var first = _target.GetOrCreate(scope, key, CreateValue, CancellationToken.None);
        var second = _target.GetOrCreate(scope, key, CreateValue, CancellationToken.None);

        first.Should().NotBeSameAs(second);
        factoryCalls.Should().Be(2);
    }

    [Fact]
    public async Task GIVEN_AsyncDisposableValue_WHEN_GettingTwice_THEN_ShouldNotCacheValue()
    {
        var scope = CreateScope(CreateSnapshotIdentity());
        var key = new TestKey("Key");
        var factoryCalls = 0;

        ValueTask<AsyncDisposableTestValue?> CreateValue(CancellationToken _)
        {
            factoryCalls++;
            return ValueTask.FromResult<AsyncDisposableTestValue?>(new AsyncDisposableTestValue());
        }

        var first = await _target.GetOrCreateAsync(scope, key, CreateValue, CancellationToken.None);
        var second = await _target.GetOrCreateAsync(scope, key, CreateValue, CancellationToken.None);

        first.Should().NotBeSameAs(second);
        factoryCalls.Should().Be(2);
    }

    [Fact]
    public void GIVEN_MultipleWorkspaceEpochs_WHEN_InvalidatingWorkspace_THEN_ShouldInvalidateOnlyMatchingEpoch()
    {
        var workspaceId = Guid.NewGuid();
        var matchingScope = CreateScope(CreateSnapshotIdentity(workspaceId, workspaceEpoch: 1));
        var otherEpochScope = CreateScope(CreateSnapshotIdentity(workspaceId, workspaceEpoch: 2));
        var otherWorkspaceScope = CreateScope(CreateSnapshotIdentity(Guid.NewGuid(), workspaceEpoch: 1));
        var key = new TestKey("Key");
        var matchingFactoryCalls = 0;
        var otherEpochFactoryCalls = 0;
        var otherWorkspaceFactoryCalls = 0;

        GetValue(matchingScope, key, ref matchingFactoryCalls);
        GetValue(otherEpochScope, key, ref otherEpochFactoryCalls);
        GetValue(otherWorkspaceScope, key, ref otherWorkspaceFactoryCalls);

        _target.InvalidateWorkspace(workspaceId, 1);

        GetValue(matchingScope, key, ref matchingFactoryCalls);
        GetValue(otherEpochScope, key, ref otherEpochFactoryCalls);
        GetValue(otherWorkspaceScope, key, ref otherWorkspaceFactoryCalls);

        matchingFactoryCalls.Should().Be(2);
        otherEpochFactoryCalls.Should().Be(1);
        otherWorkspaceFactoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_MultipleTransactions_WHEN_InvalidatingTransaction_THEN_ShouldInvalidateOnlyMatchingTransaction()
    {
        var workspaceId = Guid.NewGuid();
        var matchingTransactionId = new WorkspaceTransactionId(1);
        var matchingScope = CreateScope(CreateSnapshotIdentity(workspaceId, 1, matchingTransactionId));
        var otherTransactionScope = CreateScope(CreateSnapshotIdentity(workspaceId, 1, new WorkspaceTransactionId(2)));
        var committedScope = CreateScope(CreateSnapshotIdentity(workspaceId, 1, transactionId: null));
        var key = new TestKey("Key");
        var matchingFactoryCalls = 0;
        var otherTransactionFactoryCalls = 0;
        var committedFactoryCalls = 0;

        GetValue(matchingScope, key, ref matchingFactoryCalls);
        GetValue(otherTransactionScope, key, ref otherTransactionFactoryCalls);
        GetValue(committedScope, key, ref committedFactoryCalls);

        _target.InvalidateTransaction(workspaceId, 1, matchingTransactionId);

        GetValue(matchingScope, key, ref matchingFactoryCalls);
        GetValue(otherTransactionScope, key, ref otherTransactionFactoryCalls);
        GetValue(committedScope, key, ref committedFactoryCalls);

        matchingFactoryCalls.Should().Be(2);
        otherTransactionFactoryCalls.Should().Be(1);
        committedFactoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_SelectedSnapshots_WHEN_InvalidatingSnapshots_THEN_ShouldInvalidateEachSelection()
    {
        var firstSnapshot = CreateSnapshotIdentity();
        var secondSnapshot = CreateSnapshotIdentity();
        var retainedSnapshot = CreateSnapshotIdentity();
        var firstScope = CreateScope(firstSnapshot);
        var secondScope = CreateScope(secondSnapshot);
        var retainedScope = CreateScope(retainedSnapshot);
        var key = new TestKey("Key");
        var firstFactoryCalls = 0;
        var secondFactoryCalls = 0;
        var retainedFactoryCalls = 0;

        GetValue(firstScope, key, ref firstFactoryCalls);
        GetValue(secondScope, key, ref secondFactoryCalls);
        GetValue(retainedScope, key, ref retainedFactoryCalls);

        _target.InvalidateSnapshots([firstSnapshot, secondSnapshot]);

        GetValue(firstScope, key, ref firstFactoryCalls);
        GetValue(secondScope, key, ref secondFactoryCalls);
        GetValue(retainedScope, key, ref retainedFactoryCalls);

        firstFactoryCalls.Should().Be(2);
        secondFactoryCalls.Should().Be(2);
        retainedFactoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_ScopeVariations_WHEN_GettingSameKey_THEN_ShouldIsolateByPluginAndTool()
    {
        var snapshot = CreateSnapshotIdentity();
        var firstScope = _target.CreateScope(snapshot, "FirstPlugin", "FirstTool");
        var equivalentScope = _target.CreateScope(snapshot, "FirstPlugin", "FirstTool");
        var otherPluginScope = _target.CreateScope(snapshot, "SecondPlugin", "FirstTool");
        var otherToolScope = _target.CreateScope(snapshot, "FirstPlugin", "SecondTool");
        var key = new TestKey("Key");
        var factoryCalls = 0;

        var first = GetValue(firstScope, key, ref factoryCalls);
        var equivalent = GetValue(equivalentScope, key, ref factoryCalls);
        var otherPlugin = GetValue(otherPluginScope, key, ref factoryCalls);
        var otherTool = GetValue(otherToolScope, key, ref factoryCalls);

        equivalent.Should().BeSameAs(first);
        otherPlugin.Should().NotBeSameAs(first);
        otherTool.Should().NotBeSameAs(first);
        factoryCalls.Should().Be(3);
    }

    [Fact]
    public void GIVEN_DisposedState_WHEN_CreatingScope_THEN_ShouldThrow()
    {
        _target.Dispose();

        var action = () => CreateScope(CreateSnapshotIdentity());

        action.Should().Throw<ObjectDisposedException>();
    }

    public void Dispose()
    {
        _target.Dispose();
    }

    private QueryCacheScopeIdentity CreateScope(WorkspaceSnapshotIdentity snapshotIdentity)
    {
        return _target.CreateScope(snapshotIdentity, "PluginId", "ToolName");
    }

    private TestValue? GetValue(
        QueryCacheScopeIdentity scope,
        TestKey key,
        ref int factoryCalls)
    {
        var currentFactoryCalls = factoryCalls;
        var result = _target.GetOrCreate(
            scope,
            key,
            _ =>
            {
                currentFactoryCalls++;
                return new TestValue($"Value{currentFactoryCalls}");
            },
            CancellationToken.None);

        factoryCalls = currentFactoryCalls;
        return result;
    }

    private static WorkspaceSnapshotIdentity CreateSnapshotIdentity(
        Guid? workspaceId = null,
        long workspaceEpoch = 1,
        WorkspaceTransactionId? transactionId = null)
    {
        return new WorkspaceSnapshotIdentity(
            workspaceId ?? Guid.NewGuid(),
            workspaceEpoch,
            new WorkspaceSnapshotId(Guid.NewGuid()),
            transactionId);
    }

    private sealed record TestKey
    {
        public string Value { get; }

        public TestKey(string value)
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

    private sealed class DisposableTestValue : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class AsyncDisposableTestValue : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
