using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Contracts.Collections;

public sealed class BoundedCollectionTests
{
    [Fact]
    public void GIVEN_PreboundedItemsAndMoreResults_WHEN_CreatingCollection_THEN_ShouldPreserveItemsAndHasMore()
    {
        var result = BoundedCollection<string>.CreatePrebounded(["Item"], hasMore: true);

        result.Items.Should().Equal("Item");
        result.HasMore.Should().BeTrue();
        result.TotalCount.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PreboundedItemsAndKnownTotal_WHEN_CreatingCollection_THEN_ShouldPublishConsistentTotal()
    {
        var result = BoundedCollection<string>.CreatePrebounded(["Item"], totalCount: 3);

        result.Items.Should().Equal("Item");
        result.HasMore.Should().BeTrue();
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public void GIVEN_AllItemsAreReturned_WHEN_CreatingCollection_THEN_ShouldPublishReturnedCountAsTotal()
    {
        var result = BoundedCollection<string>.Create(["First", "Second"]);

        result.Items.Should().Equal("First", "Second");
        result.HasMore.Should().BeFalse();
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public void GIVEN_NoPreboundedItemsAndNoMoreResults_WHEN_CreatingCollection_THEN_ShouldReturnEmptyCollection()
    {
        var result = BoundedCollection<string>.CreatePrebounded([], hasMore: false);

        result.Should().BeSameAs(BoundedCollection<string>.Empty());
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public void GIVEN_NoMoreResults_WHEN_CreatingPreboundedCollection_THEN_ShouldPublishReturnedCountAsTotal()
    {
        var result = BoundedCollection<string>.CreatePrebounded(["Item"], hasMore: false);

        result.HasMore.Should().BeFalse();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public void GIVEN_UnknownTotal_WHEN_SerializingCollection_THEN_ShouldOmitTotalCount()
    {
        var target = BoundedCollection<string>.CreatePrebounded(["Item"], hasMore: true);

        var result = JsonSerializer.SerializeToElement(target, JsonSerializerOptions.Web);

        result.TryGetProperty("totalCount", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_KnownTotalBelowReturnedCount_WHEN_CreatingCollection_THEN_ShouldThrow()
    {
        var action = () => BoundedCollection<string>.CreatePrebounded(["First", "Second"], totalCount: 1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

}
