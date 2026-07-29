using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Results;

public sealed class BoundedCollectionTests
{
    [Fact]
    public void GIVEN_PreboundedItemsAndMoreResults_WHEN_CreatingCollection_THEN_ShouldPreserveItemsAndHasMore()
    {
        var result = BoundedCollection.CreatePrebounded(["Item"], hasMore: true);

        result.Items.Should().Equal("Item");
        result.HasMore.Should().BeTrue();
        result.TotalCount.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PreboundedItemsAndKnownTotal_WHEN_CreatingCollection_THEN_ShouldPublishConsistentTotal()
    {
        var result = BoundedCollection.CreatePrebounded(["Item"], totalCount: 3);

        result.Items.Should().Equal("Item");
        result.HasMore.Should().BeTrue();
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public void GIVEN_NoPreboundedItemsAndNoMoreResults_WHEN_CreatingCollection_THEN_ShouldReturnEmptyCollection()
    {
        var result = BoundedCollection.CreatePrebounded<string>([], hasMore: false);

        result.Should().BeSameAs(BoundedCollection.Empty<string>());
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public void GIVEN_NoReturnedItemsAndMoreResults_WHEN_CreatingCollection_THEN_ShouldPreserveTruncation()
    {
        var result = BoundedCollection.CreatePrebounded<string>([], hasMore: true);

        result.Items.Should().BeEmpty();
        result.HasMore.Should().BeTrue();
        result.TotalCount.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NoMoreResults_WHEN_CreatingPreboundedCollection_THEN_ShouldPublishItemCountAsTotal()
    {
        var result = BoundedCollection.CreatePrebounded(["Item"], hasMore: false);

        result.HasMore.Should().BeFalse();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public void GIVEN_KnownZeroTotal_WHEN_CreatingCollection_THEN_ShouldReturnEmptyCollection()
    {
        var result = BoundedCollection.CreatePrebounded<string>([], totalCount: 0);

        result.Should().BeSameAs(BoundedCollection.Empty<string>());
    }

    [Fact]
    public void GIVEN_KnownTotalMatchesItemCount_WHEN_CreatingCollection_THEN_ShouldReportCompleteResult()
    {
        var result = BoundedCollection.CreatePrebounded(["First", "Second"], totalCount: 2);

        result.Items.Should().Equal("First", "Second");
        result.HasMore.Should().BeFalse();
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public void GIVEN_UnknownTotal_WHEN_SerializingCollection_THEN_ShouldOmitTotalCount()
    {
        var target = BoundedCollection.CreatePrebounded(["Item"], hasMore: true);

        var result = JsonSerializer.SerializeToElement(target, JsonSerializerOptions.Web);

        result.TryGetProperty("totalCount", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_KnownTotalBelowItemCount_WHEN_CreatingCollection_THEN_ShouldThrow()
    {
        var action = () => BoundedCollection.CreatePrebounded(["First", "Second"], totalCount: 1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GIVEN_NullItems_WHEN_CreatingCollectionWithKnownTotal_THEN_ShouldThrow()
    {
        IReadOnlyList<string>? items = null;

        var action = () => BoundedCollection.CreatePrebounded(items!, totalCount: 0);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullItems_WHEN_CreatingCollectionWithUnknownTotal_THEN_ShouldThrow()
    {
        IReadOnlyList<string>? items = null;

        var action = () => BoundedCollection.CreatePrebounded(items!, hasMore: false);

        action.Should().Throw<ArgumentNullException>();
    }
}
