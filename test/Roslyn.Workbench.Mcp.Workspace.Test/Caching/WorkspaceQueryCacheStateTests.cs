using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class WorkspaceQueryCacheStateTests : IDisposable
{
    private readonly WorkspaceQueryCacheState _target;

    public WorkspaceQueryCacheStateTests()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        var options = Options.Create(new WorkspaceQueryCacheOptions
        {
            SizeLimit = 100,
            SlidingExpiration = TimeSpan.FromHours(1),
        });

        _target = new WorkspaceQueryCacheState(options, applicationLifetime.Object);
    }

    [Fact]
    public void GIVEN_AdmittedValue_WHEN_GettingSynchronouslyTwice_THEN_ShouldReuseCachedValue()
    {
        var scope = CreateScope("ComponentIdentity");
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
            static _ => 1,
            static _ => true,
            CancellationToken.None);

        var second = _target.GetOrCreate(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return new TestValue("Unexpected");
            },
            static _ => 1,
            static _ => true,
            CancellationToken.None);

        first.Should().BeSameAs(expected);
        second.Should().BeSameAs(expected);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_AdmittedValue_WHEN_GettingAsynchronouslyTwice_THEN_ShouldReuseCachedValue()
    {
        var scope = CreateScope("ComponentIdentity");
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
            static _ => 1,
            static _ => true,
            CancellationToken.None);

        var second = await _target.GetOrCreateAsync(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult<TestValue?>(new TestValue("Unexpected"));
            },
            static _ => 1,
            static _ => true,
            CancellationToken.None);

        first.Should().BeSameAs(expected);
        second.Should().BeSameAs(expected);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_ProjectedResult_WHEN_GettingTwice_THEN_ShouldReconstructFromCachedValue()
    {
        var scope = CreateScope("ComponentIdentity");
        var key = new TestKey("Key");
        var expected = new TestResult("Value", "FirstMetadata");
        var factoryCalls = 0;

        var first = _target.GetOrCreateProjected<TestKey, TestValue, TestResult>(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return expected;
            },
            static result => new TestValue(result.Value),
            static value => new TestResult(value.Value, "CachedMetadata"),
            static _ => 1,
            CancellationToken.None);

        var second = _target.GetOrCreateProjected<TestKey, TestValue, TestResult>(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return new TestResult("Unexpected", "UnexpectedMetadata");
            },
            static result => new TestValue(result.Value),
            static value => new TestResult(value.Value, "CachedMetadata"),
            static _ => 1,
            CancellationToken.None);

        first.Should().BeSameAs(expected);
        second.Value.Should().Be("Value");
        second.Metadata.Should().Be("CachedMetadata");
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_StoredValue_WHEN_Probing_THEN_ShouldReturnValue()
    {
        var scope = CreateScope("ComponentIdentity");
        var key = new TestKey("Key");
        var expected = new TestValue("Value");

        _target.Store(scope, key, expected, static _ => 1);

        var found = _target.TryGet<TestKey, TestValue>(scope, key, out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void GIVEN_InvalidatedWorkspace_WHEN_UsingOldAndNewScopes_THEN_ShouldRejectOldGeneration()
    {
        var workspaceId = Guid.NewGuid();
        var solution = MutationCandidateTestData.Solution;
        var oldScope = _target.CreateScope(workspaceId, solution, "ComponentIdentity");
        var key = new TestKey("Key");
        var oldValue = new TestValue("OldValue");
        _target.Store(oldScope, key, oldValue, static _ => 1);

        _target.InvalidateWorkspace(workspaceId);
        _target.Store(oldScope, key, new TestValue("RejectedValue"), static _ => 1);
        var oldFound = _target.TryGet<TestKey, TestValue>(oldScope, key, out var oldResult);

        var newScope = _target.CreateScope(workspaceId, solution, "ComponentIdentity");
        var newValue = new TestValue("NewValue");
        _target.Store(newScope, key, newValue, static _ => 1);
        var newFound = _target.TryGet<TestKey, TestValue>(newScope, key, out var newResult);

        oldFound.Should().BeFalse();
        oldResult.Should().BeNull();
        newFound.Should().BeTrue();
        newResult.Should().BeSameAs(newValue);
    }

    [Fact]
    public void GIVEN_ScopeVariations_WHEN_GettingSameKey_THEN_ShouldIsolateBySolutionAndComponent()
    {
        var workspaceId = Guid.NewGuid();
        var firstSolution = MutationCandidateTestData.Solution;
        var secondProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "SecondProject",
            "SecondProject",
            LanguageNames.CSharp);
        var secondSolution = firstSolution.AddProject(secondProjectInfo);
        var firstScope = _target.CreateScope(workspaceId, firstSolution, "FirstComponent");
        var equivalentScope = _target.CreateScope(workspaceId, firstSolution, "FirstComponent");
        var differentSolutionScope = _target.CreateScope(workspaceId, secondSolution, "FirstComponent");
        var differentComponentScope = _target.CreateScope(workspaceId, firstSolution, "SecondComponent");
        var key = new TestKey("Key");
        var factoryCalls = 0;

        TestValue? GetValue(QueryCacheScopeIdentity scope)
        {
            return _target.GetOrCreate(
                scope,
                key,
                _ =>
                {
                    factoryCalls++;
                    return new TestValue($"Value{factoryCalls}");
                },
                static _ => 1,
                static _ => true,
                CancellationToken.None);
        }

        var first = GetValue(firstScope);
        var equivalent = GetValue(equivalentScope);
        var differentSolution = GetValue(differentSolutionScope);
        var differentComponent = GetValue(differentComponentScope);

        equivalent.Should().BeSameAs(first);
        differentSolution.Should().NotBeSameAs(first);
        differentComponent.Should().NotBeSameAs(first);
        factoryCalls.Should().Be(3);
    }

    [Fact]
    public void GIVEN_DisposedState_WHEN_CreatingScope_THEN_ShouldThrow()
    {
        _target.Dispose();

        var action = () => CreateScope("ComponentIdentity");

        action.Should().Throw<ObjectDisposedException>();
    }

    public void Dispose()
    {
        _target.Dispose();
    }

    private QueryCacheScopeIdentity CreateScope(string componentIdentity)
    {
        return _target.CreateScope(
            Guid.NewGuid(),
            MutationCandidateTestData.Solution,
            componentIdentity);
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

        public string Metadata { get; }

        public TestResult(string value, string metadata)
        {
            Value = value;
            Metadata = metadata;
        }
    }
}
