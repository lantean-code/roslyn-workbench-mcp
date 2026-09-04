using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class WorkspaceQueryCacheStoreTests
{
    private readonly Mock<IWorkspaceQueryCacheState> _state;
    private readonly WorkspaceQueryCacheStore _target;

    public WorkspaceQueryCacheStoreTests()
    {
        _state = new Mock<IWorkspaceQueryCacheState>();
        _target = new WorkspaceQueryCacheStore(_state.Object);
    }

    [Fact]
    public void GIVEN_ScopeDetails_WHEN_CreatingScope_THEN_ShouldDelegateToState()
    {
        var workspaceId = Guid.NewGuid();
        var solution = MutationCandidateTestData.Solution;
        var expected = CreateScopeIdentity();
        _state
            .Setup(item => item.CreateScope(workspaceId, solution, "ComponentIdentity"))
            .Returns(expected);

        var result = _target.CreateScope(workspaceId, solution, "ComponentIdentity");

        result.Should().BeSameAs(expected);
        _state.Verify(item => item.CreateScope(workspaceId, solution, "ComponentIdentity"), Times.Once);
    }

    [Fact]
    public void GIVEN_SynchronousFactory_WHEN_GettingValue_THEN_ShouldDelegateToState()
    {
        var scopeIdentity = CreateScopeIdentity();
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        Func<CancellationToken, TestValue?> valueFactory = static _ => new TestValue("FactoryValue");
        Func<TestValue, long> sizeCalculator = static _ => 1;
        Func<TestValue, bool> admissionPredicate = static _ => true;
        var cancellationToken = new CancellationToken(canceled: false);
        _state
            .Setup(item => item.GetOrCreate(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken))
            .Returns(expected);

        var result = _target.GetOrCreate(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken);

        result.Should().BeSameAs(expected);
        _state.Verify(item => item.GetOrCreate(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AsynchronousFactory_WHEN_GettingValue_THEN_ShouldDelegateToState()
    {
        var scopeIdentity = CreateScopeIdentity();
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        Func<CancellationToken, ValueTask<TestValue?>> valueFactory = static _ => ValueTask.FromResult<TestValue?>(new TestValue("FactoryValue"));
        Func<TestValue, long> sizeCalculator = static _ => 1;
        Func<TestValue, bool> admissionPredicate = static _ => true;
        var cancellationToken = new CancellationToken(canceled: false);
        _state
            .Setup(item => item.GetOrCreateAsync(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken))
            .Returns(() => ValueTask.FromResult<TestValue?>(expected));

        var result = await _target.GetOrCreateAsync(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken);

        result.Should().BeSameAs(expected);
        _state.Verify(item => item.GetOrCreateAsync(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken), Times.Once);
    }

    [Fact]
    public void GIVEN_ProjectedFactory_WHEN_GettingResult_THEN_ShouldDelegateToState()
    {
        var scopeIdentity = CreateScopeIdentity();
        var key = new TestKey("Key");
        var expected = new TestResult("Value");
        Func<CancellationToken, TestResult> resultFactory = static _ => new TestResult("FactoryValue");
        Func<TestResult, TestValue?> cacheValueSelector = static result => new TestValue(result.Value);
        Func<TestValue, TestResult> cachedResultSelector = static value => new TestResult(value.Value);
        Func<TestValue, long> sizeCalculator = static _ => 1;
        var cancellationToken = new CancellationToken(canceled: false);
        _state
            .Setup(item => item.GetOrCreateProjected(scopeIdentity, key, resultFactory, cacheValueSelector, cachedResultSelector, sizeCalculator, cancellationToken))
            .Returns(expected);

        var result = _target.GetOrCreateProjected(scopeIdentity, key, resultFactory, cacheValueSelector, cachedResultSelector, sizeCalculator, cancellationToken);

        result.Should().BeSameAs(expected);
        _state.Verify(item => item.GetOrCreateProjected(scopeIdentity, key, resultFactory, cacheValueSelector, cachedResultSelector, sizeCalculator, cancellationToken), Times.Once);
    }

    [Fact]
    public void GIVEN_StoredValue_WHEN_Probing_THEN_ShouldDelegateToState()
    {
        var scopeIdentity = CreateScopeIdentity();
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        TestValue? stateValue = expected;
        _state
            .Setup(item => item.TryGet<TestKey, TestValue>(scopeIdentity, key, out stateValue))
            .Returns(true);

        var found = _target.TryGet<TestKey, TestValue>(scopeIdentity, key, out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(expected);
        _state.Verify(item => item.TryGet<TestKey, TestValue>(scopeIdentity, key, out stateValue), Times.Once);
    }

    [Fact]
    public void GIVEN_Value_WHEN_Storing_THEN_ShouldDelegateToState()
    {
        var scopeIdentity = CreateScopeIdentity();
        var key = new TestKey("Key");
        var value = new TestValue("Value");
        Func<TestValue, long> sizeCalculator = static _ => 1;

        _target.Store(scopeIdentity, key, value, sizeCalculator);

        _state.Verify(item => item.Store(scopeIdentity, key, value, sizeCalculator), Times.Once);
    }

    private static QueryCacheScopeIdentity CreateScopeIdentity()
    {
        var generation = new QueryCacheGeneration("Partition", CancellationToken.None);
        return new QueryCacheScopeIdentity(generation, "Scope");
    }

    private sealed record TestKey : IWorkspaceQueryCacheKey
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

    private sealed record TestResult
    {
        public string Value { get; }

        public TestResult(string value)
        {
            Value = value;
        }
    }
}
