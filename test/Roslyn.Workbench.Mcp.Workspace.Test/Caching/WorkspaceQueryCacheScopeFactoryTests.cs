using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class WorkspaceQueryCacheScopeFactoryTests
{
    private readonly Mock<IWorkspaceQueryCacheStore> _store;
    private readonly WorkspaceQueryCacheScopeFactory _target;

    public WorkspaceQueryCacheScopeFactoryTests()
    {
        _store = new Mock<IWorkspaceQueryCacheStore>();
        _target = new WorkspaceQueryCacheScopeFactory(_store.Object);
    }

    [Fact]
    public void GIVEN_ScopeDetails_WHEN_CreatingScope_THEN_ShouldCreateStoreScope()
    {
        var workspaceId = Guid.NewGuid();
        var solution = MutationCandidateTestData.Solution;
        var scopeIdentity = CreateScopeIdentity();
        _store
            .Setup(item => item.CreateScope(workspaceId, solution, "ComponentIdentity"))
            .Returns(scopeIdentity);

        var result = _target.CreateScope(workspaceId, solution, "ComponentIdentity");

        result.Should().NotBeNull();
        _store.Verify(item => item.CreateScope(workspaceId, solution, "ComponentIdentity"), Times.Once);
    }

    [Fact]
    public void GIVEN_CreatedScope_WHEN_GettingSynchronousValue_THEN_ShouldUseCreatedIdentity()
    {
        var scopeIdentity = CreateScopeIdentity();
        var scope = CreateScope(scopeIdentity);
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        Func<CancellationToken, TestValue?> valueFactory = static _ => new TestValue("FactoryValue");
        Func<TestValue, long> sizeCalculator = static _ => 1;
        Func<TestValue, bool> admissionPredicate = static _ => true;
        var cancellationToken = new CancellationToken(canceled: false);
        _store
            .Setup(item => item.GetOrCreate(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken))
            .Returns(expected);

        var result = scope.GetOrCreate(key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken);

        result.Should().BeSameAs(expected);
        _store.Verify(item => item.GetOrCreate(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CreatedScope_WHEN_GettingAsynchronousValue_THEN_ShouldUseCreatedIdentity()
    {
        var scopeIdentity = CreateScopeIdentity();
        var scope = CreateScope(scopeIdentity);
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        Func<CancellationToken, ValueTask<TestValue?>> valueFactory = static _ => ValueTask.FromResult<TestValue?>(new TestValue("FactoryValue"));
        Func<TestValue, long> sizeCalculator = static _ => 1;
        Func<TestValue, bool> admissionPredicate = static _ => true;
        var cancellationToken = new CancellationToken(canceled: false);
        _store
            .Setup(item => item.GetOrCreateAsync(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken))
            .Returns(() => ValueTask.FromResult<TestValue?>(expected));

        var result = await scope.GetOrCreateAsync(key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken);

        result.Should().BeSameAs(expected);
        _store.Verify(item => item.GetOrCreateAsync(scopeIdentity, key, valueFactory, sizeCalculator, admissionPredicate, cancellationToken), Times.Once);
    }

    [Fact]
    public void GIVEN_CreatedScope_WHEN_GettingProjectedResult_THEN_ShouldUseCreatedIdentity()
    {
        var scopeIdentity = CreateScopeIdentity();
        var scope = CreateScope(scopeIdentity);
        var key = new TestKey("Key");
        var expected = new TestResult("Value");
        Func<CancellationToken, TestResult> resultFactory = static _ => new TestResult("FactoryValue");
        Func<TestResult, TestValue?> cacheValueSelector = static result => new TestValue(result.Value);
        Func<TestValue, TestResult> cachedResultSelector = static value => new TestResult(value.Value);
        Func<TestValue, long> sizeCalculator = static _ => 1;
        var cancellationToken = new CancellationToken(canceled: false);
        _store
            .Setup(item => item.GetOrCreateProjected(scopeIdentity, key, resultFactory, cacheValueSelector, cachedResultSelector, sizeCalculator, cancellationToken))
            .Returns(expected);

        var result = scope.GetOrCreateProjected(key, resultFactory, cacheValueSelector, cachedResultSelector, sizeCalculator, cancellationToken);

        result.Should().BeSameAs(expected);
        _store.Verify(item => item.GetOrCreateProjected(scopeIdentity, key, resultFactory, cacheValueSelector, cachedResultSelector, sizeCalculator, cancellationToken), Times.Once);
    }

    [Fact]
    public void GIVEN_CreatedScope_WHEN_ProbingValue_THEN_ShouldUseCreatedIdentity()
    {
        var scopeIdentity = CreateScopeIdentity();
        var scope = CreateScope(scopeIdentity);
        var key = new TestKey("Key");
        var expected = new TestValue("Value");
        TestValue? storeValue = expected;
        _store
            .Setup(item => item.TryGet<TestKey, TestValue>(scopeIdentity, key, out storeValue))
            .Returns(true);

        var found = scope.TryGet<TestKey, TestValue>(key, out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(expected);
        _store.Verify(item => item.TryGet<TestKey, TestValue>(scopeIdentity, key, out storeValue), Times.Once);
    }

    [Fact]
    public void GIVEN_CreatedScope_WHEN_StoringValue_THEN_ShouldUseCreatedIdentity()
    {
        var scopeIdentity = CreateScopeIdentity();
        var scope = CreateScope(scopeIdentity);
        var key = new TestKey("Key");
        var value = new TestValue("Value");
        Func<TestValue, long> sizeCalculator = static _ => 1;

        scope.Store(key, value, sizeCalculator);

        _store.Verify(item => item.Store(scopeIdentity, key, value, sizeCalculator), Times.Once);
    }

    private IWorkspaceQueryCacheScope CreateScope(QueryCacheScopeIdentity scopeIdentity)
    {
        var workspaceId = Guid.NewGuid();
        var solution = MutationCandidateTestData.Solution;
        _store
            .Setup(item => item.CreateScope(workspaceId, solution, "ComponentIdentity"))
            .Returns(scopeIdentity);

        return _target.CreateScope(workspaceId, solution, "ComponentIdentity");
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
