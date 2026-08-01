using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.References;

public sealed class CodeActionReferenceStoreTests : IDisposable
{
    private static readonly DateTimeOffset _utcNow = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISystemClock> _clock;
    private readonly Mock<TimeProvider> _timeProvider;
    private readonly MemoryCache _cache;
    private readonly CodeActionReferenceState _target;

    public CodeActionReferenceStoreTests()
    {
        _clock = new Mock<ISystemClock>();
        _timeProvider = new Mock<TimeProvider>();
        _clock.SetupGet(item => item.UtcNow).Returns(_utcNow);
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = _clock.Object,
            SizeLimit = 100_000,
        });

        _target = new CodeActionReferenceState(_cache, _timeProvider.Object);
    }

    public void Dispose()
    {
        _target.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public void GIVEN_AvailableCapacity_WHEN_CreatingReference_THEN_ShouldStoreRecipeUntilAbsoluteExpiry()
    {
        var recipe = CreateRecipe();
        var expiresAt = _utcNow.AddMinutes(5);

        var created = _target.TryCreate(recipe, expiresAt, out var reference);

        created.Should().BeTrue();
        reference.Should().NotBeNull();
        reference.ActionId.Should().NotBe(Guid.Empty);
        reference.Recipe.Should().BeSameAs(recipe);
        reference.ExpiresAt.Should().Be(expiresAt);
        _target.TryGet(reference.ActionId, out var stored).Should().BeTrue();
        stored.Should().BeSameAs(reference);
    }

    [Fact]
    public void GIVEN_ReferenceHasExpired_WHEN_GettingReference_THEN_ShouldRemoveIt()
    {
        var expiresAt = _utcNow.AddMinutes(5);
        _target.TryCreate(CreateRecipe(), expiresAt, out var reference).Should().BeTrue();
        var actionId = reference.Should().BeOfType<CodeActionReference>().Which.ActionId;
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(expiresAt.AddTicks(1));

        var found = _target.TryGet(actionId, out var result);

        found.Should().BeFalse();
        result.Should().BeNull();
        _target.TryGet(actionId, out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_CacheCannotAcceptRecipeSize_WHEN_CreatingReference_THEN_ShouldReturnFalse()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = _clock.Object,
            SizeLimit = 1,
        });

        using var target = new CodeActionReferenceState(cache, _timeProvider.Object);
        var recipe = CreateRecipe() with { Title = "Title" };

        var created = target.TryCreate(recipe, _utcNow.AddMinutes(5), out var reference);

        created.Should().BeFalse();
        reference.Should().BeNull();
    }

    [Fact]
    public void GIVEN_StoredReference_WHEN_RemovingReference_THEN_ShouldNoLongerResolve()
    {
        _target.TryCreate(CreateRecipe(), _utcNow.AddMinutes(5), out var reference).Should().BeTrue();
        var actionId = reference.Should().BeOfType<CodeActionReference>().Which.ActionId;

        _target.Remove(actionId);

        _target.TryGet(actionId, out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_PreparedFixAllReferenceHasExpired_WHEN_CheckingReferenceKind_THEN_ShouldRemoveIt()
    {
        var expiresAt = _utcNow.AddMinutes(5);
        var recipe = CreateRecipe() with
        {
            PreparedFixAllScope = CodeActionFixAllScope.Solution,
        };

        _target.TryCreate(recipe, expiresAt, out var reference).Should().BeTrue();
        var actionId = reference.Should().BeOfType<CodeActionReference>().Which.ActionId;
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(expiresAt.AddTicks(1));

        _target.IsPreparedFixAll(actionId).Should().BeFalse();
        _target.TryGet(actionId, out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_SingleAndPreparedReferences_WHEN_CheckingReferenceKind_THEN_ShouldIdentifyOnlyPreparedFixAll()
    {
        var single = CreateReference(CreateRecipe());
        var prepared = CreateReference(CreateRecipe() with
        {
            PreparedFixAllScope = CodeActionFixAllScope.Project,
        });

        _target.IsPreparedFixAll(single.ActionId).Should().BeFalse();
        _target.IsPreparedFixAll(prepared.ActionId).Should().BeTrue();
        _target.IsPreparedFixAll(Guid.Empty).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_CacheEntryWasRemovedWithoutCallback_WHEN_GettingReference_THEN_ShouldRemoveRegistration()
    {
        var cache = new Mock<IMemoryCache>();
        var cacheEntry = new Mock<ICacheEntry>();
        var cachedValueAvailable = true;

        cacheEntry.SetupAllProperties();
        cacheEntry.SetupGet(item => item.ExpirationTokens).Returns([]);
        cacheEntry.SetupGet(item => item.PostEvictionCallbacks).Returns([]);
        cache.Setup(item => item.CreateEntry(It.IsAny<object>())).Returns(cacheEntry.Object);
        cache.Setup(item => item.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
            .Returns((object _, out object? value) =>
            {
                value = cachedValueAvailable
                    ? cacheEntry.Object.Value
                    : null;

                cachedValueAvailable = false;
                return value is not null;
            });

        using var target = new CodeActionReferenceState(cache.Object, _timeProvider.Object);
        target.TryCreate(
            CreateRecipe(),
            _utcNow.AddMinutes(5),
            out var reference).Should().BeTrue();

        var actionId = reference.Should().BeOfType<CodeActionReference>().Which.ActionId;

        target.TryGet(actionId, out _).Should().BeFalse();
        cachedValueAvailable = true;
        target.TryGet(actionId, out _).Should().BeFalse();

        cache.Verify(
            item => item.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny),
            Times.Exactly(2));
    }

    [Fact]
    public void GIVEN_ReferencesFromSeveralSnapshots_WHEN_InvalidatingOneSnapshot_THEN_ShouldRemoveOnlyMatchingReferences()
    {
        var first = CreateReference(CreateRecipe(snapshotId: 2));
        var second = CreateReference(CreateRecipe(snapshotId: 3));

        _target.InvalidateSnapshots([CreateSnapshotIdentity(snapshotId: 2)]);

        _target.TryGet(first.ActionId, out _).Should().BeFalse();
        _target.TryGet(second.ActionId, out _).Should().BeTrue();
        _cache.Count.Should().Be(1);
    }

    [Fact]
    public void GIVEN_TransactionAndCommittedReferences_WHEN_InvalidatingTransaction_THEN_ShouldRetainCommittedReference()
    {
        var first = CreateReference(CreateRecipe(snapshotId: 2));
        var second = CreateReference(CreateRecipe(snapshotId: 3));
        var committed = CreateReference(CreateRecipe(snapshotId: 1, transactionId: null));

        _target.InvalidateTransaction(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            new WorkspaceTransactionId(1));

        _target.TryGet(first.ActionId, out _).Should().BeFalse();
        _target.TryGet(second.ActionId, out _).Should().BeFalse();
        _target.TryGet(committed.ActionId, out _).Should().BeTrue();
        _cache.Count.Should().Be(1);
    }

    [Fact]
    public void GIVEN_ReferencesFromSeveralWorkspaceEpochs_WHEN_InvalidatingWorkspace_THEN_ShouldRemoveOnlyMatchingInstance()
    {
        var first = CreateReference(CreateRecipe(workspaceEpoch: 1));
        var second = CreateReference(CreateRecipe(workspaceEpoch: 2));

        _target.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1);

        _target.TryGet(first.ActionId, out _).Should().BeFalse();
        _target.TryGet(second.ActionId, out _).Should().BeTrue();
        _cache.Count.Should().Be(1);
    }

    [Fact]
    public void GIVEN_RepeatedDiscovery_WHEN_InvalidatingLifecycleScopes_THEN_ShouldActivelyReleaseCacheCapacity()
    {
        var references = new List<CodeActionReference>();
        for (var index = 1; index <= 100; index++)
        {
            references.Add(CreateReference(CreateRecipe(snapshotId: index)));
        }

        _cache.Count.Should().Be(100);

        _target.InvalidateTransaction(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            new WorkspaceTransactionId(1));

        _cache.Count.Should().Be(0);
        foreach (var reference in references)
        {
            _target.TryGet(reference.ActionId, out _).Should().BeFalse();
        }
    }

    [Fact]
    public void GIVEN_ConcurrentLifecycleInvalidations_WHEN_RemovingReferences_THEN_ShouldRemainConsistent()
    {
        var references = new List<CodeActionReference>();
        for (var index = 1; index <= 100; index++)
        {
            references.Add(CreateReference(CreateRecipe(snapshotId: index)));
        }

        Parallel.Invoke(
            () => _target.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1),
            () => _target.InvalidateTransaction(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                1,
                new WorkspaceTransactionId(1)),
            () => _target.InvalidateSnapshots(
                Enumerable.Range(1, 100)
                    .Select(index => CreateSnapshotIdentity(index))
                    .ToArray()));

        _cache.Count.Should().Be(0);
        foreach (var reference in references)
        {
            _target.TryGet(reference.ActionId, out _).Should().BeFalse();
        }
    }

    [Fact]
    public void GIVEN_ConcurrentDiscoveryAndInvalidation_WHEN_FinalScopeInvalidationCompletes_THEN_ShouldReleaseAllReferences()
    {
        const int referenceCount = 500;
        var references = new CodeActionReference?[referenceCount];

        Parallel.Invoke(
            () => Parallel.For(0, referenceCount, index =>
            {
                _target.TryCreate(
                    CreateRecipe(snapshotId: (index % 10) + 1),
                    _utcNow.AddMinutes(5),
                    out references[index]).Should().BeTrue();
            }),
            () => Parallel.For(0, 20, _ => _target.InvalidateTransaction(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                1,
                new WorkspaceTransactionId(1))));

        _target.InvalidateTransaction(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            new WorkspaceTransactionId(1));

        _cache.Count.Should().Be(0);
        foreach (var reference in references)
        {
            reference.Should().NotBeNull();
            _target.TryGet(reference!.ActionId, out _).Should().BeFalse();
        }
    }

    [Fact]
    public void GIVEN_RepeatedDiscoveryAndInvalidationCycles_WHEN_EachCycleCompletes_THEN_ShouldNotAccumulateReferences()
    {
        for (var cycle = 1; cycle <= 50; cycle++)
        {
            for (var index = 1; index <= 20; index++)
            {
                CreateReference(CreateRecipe(snapshotId: index));
            }

            _target.InvalidateTransaction(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                1,
                new WorkspaceTransactionId(1));

            _cache.Count.Should().Be(0);
        }
    }

    private CodeActionReference CreateReference(CodeActionReplayRecipe recipe)
    {
        _target.TryCreate(
            recipe,
            _utcNow.AddMinutes(5),
            out var reference).Should().BeTrue();

        return reference.Should().BeOfType<CodeActionReference>().Which;
    }

    private static CodeActionReplayRecipe CreateRecipe(
        long snapshotId = 1,
        long? transactionId = 1,
        long workspaceEpoch = 1)
    {
        return CodeActionExecutionTestFactory.CreateReplayRecipe() with
        {
            ActionPath = [1],
            DiagnosticIds = ["DiagnosticId"],
            SnapshotIdentity = CreateSnapshotIdentity(
                snapshotId,
                transactionId,
                workspaceEpoch),
        };
    }

    private static WorkspaceSnapshotIdentity CreateSnapshotIdentity(
        long snapshotId,
        long? transactionId = 1,
        long workspaceEpoch = 1)
    {
        WorkspaceTransactionId? typedTransactionId = null;
        if (transactionId is not null)
        {
            typedTransactionId = new WorkspaceTransactionId(transactionId.Value);
        }

        return new WorkspaceSnapshotIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            workspaceEpoch,
            new WorkspaceSnapshotId(snapshotId),
            typedTransactionId);
    }
}
