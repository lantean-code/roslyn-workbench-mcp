namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Contracts.Collections;

public sealed class BoundedCollectionTests
{
    [Fact]
    public void GIVEN_PreboundedItemsAndMoreResults_WHEN_CreatingCollection_THEN_ShouldPreserveItemsAndHasMore()
    {
        var result = BoundedCollection<string>.CreatePrebounded(["Item"], hasMore: true);

        result.Items.Should().Equal("Item");
        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_NoPreboundedItemsAndNoMoreResults_WHEN_CreatingCollection_THEN_ShouldReturnEmptyCollection()
    {
        var result = BoundedCollection<string>.CreatePrebounded([], hasMore: false);

        result.Should().BeSameAs(BoundedCollection<string>.Empty());
    }

    [Fact]
    public void GIVEN_NullPreboundedItems_WHEN_CreatingCollection_THEN_ShouldThrow()
    {
        var action = () => BoundedCollection<string>.CreatePrebounded(null!, hasMore: false);

        action.Should().Throw<ArgumentNullException>();
    }
}
