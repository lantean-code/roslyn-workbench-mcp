namespace Roslyn.Workbench.Mcp.Test.UserInteraction;

public sealed class UserInteractionResultTests
{
    [Fact]
    public void GIVEN_SelectedValue_WHEN_CreatingAcceptedResult_THEN_ShouldExposeAcceptedInvariant()
    {
        var result = UserInteractionResult.Accepted("Value");

        result.Outcome.Should().Be(UserInteractionOutcome.Accepted);
        result.IsAccepted.Should().BeTrue();
        result.SelectedValue.Should().Be("Value");
    }

    [Theory]
    [InlineData((int)UserInteractionOutcome.Declined)]
    [InlineData((int)UserInteractionOutcome.Cancelled)]
    [InlineData((int)UserInteractionOutcome.Unavailable)]
    [InlineData((int)UserInteractionOutcome.Failed)]
    [InlineData((int)UserInteractionOutcome.InvalidResponse)]
    public void GIVEN_NonAcceptedOutcome_WHEN_CreatingResult_THEN_ShouldExposeNoSelectedValue(int outcomeValue)
    {
        var outcome = (UserInteractionOutcome)outcomeValue;

        var result = UserInteractionResult.NotAccepted(outcome);

        result.Outcome.Should().Be(outcome);
        result.IsAccepted.Should().BeFalse();
        result.SelectedValue.Should().BeNull();
    }

    [Fact]
    public void GIVEN_AcceptedOutcomeWithoutValue_WHEN_CreatingResult_THEN_ShouldRejectInvalidInvariant()
    {
        var action = () => UserInteractionResult.NotAccepted(UserInteractionOutcome.Accepted);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GIVEN_InteractionOutcomes_WHEN_ReadingValues_THEN_ShouldRetainExplicitNumericContract()
    {
        ((int)UserInteractionOutcome.Accepted).Should().Be(0);
        ((int)UserInteractionOutcome.Declined).Should().Be(1);
        ((int)UserInteractionOutcome.Cancelled).Should().Be(2);
        ((int)UserInteractionOutcome.Unavailable).Should().Be(3);
        ((int)UserInteractionOutcome.Failed).Should().Be(4);
        ((int)UserInteractionOutcome.InvalidResponse).Should().Be(5);
    }
}
