using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.References;

public sealed class CodeActionReferenceStoreTests : IDisposable
{
    private static readonly DateTimeOffset _utcNow = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISystemClock> _clock;
    private readonly Mock<TimeProvider> _timeProvider;
    private readonly MemoryCache _cache;
    private readonly CodeActionReferenceStore _target;

    public CodeActionReferenceStoreTests()
    {
        _clock = new Mock<ISystemClock>();
        _timeProvider = new Mock<TimeProvider>();
        _clock.SetupGet(item => item.UtcNow).Returns(_utcNow);
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = _clock.Object,
            SizeLimit = 1_000,
        });

        _target = new CodeActionReferenceStore(_cache, _timeProvider.Object);
    }

    public void Dispose()
    {
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

        var target = new CodeActionReferenceStore(cache, _timeProvider.Object);
        var recipe = new CodeActionReplayRecipe
        {
            Title = "Title",
        };

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

    private static CodeActionReplayRecipe CreateRecipe()
    {
        return new CodeActionReplayRecipe
        {
            ActionPath = [1],
            DiagnosticIds = ["DiagnosticId"],
        };
    }
}
