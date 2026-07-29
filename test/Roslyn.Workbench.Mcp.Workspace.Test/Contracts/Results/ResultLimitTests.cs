namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Results;

public sealed class ResultLimitTests
{
    [Fact]
    public void GIVEN_NoRequestedLimit_WHEN_GettingEffectiveValue_THEN_ShouldReturnDefault()
    {
        var result = ResultLimit.GetEffectiveValue(requestedLimit: null, defaultLimit: 25);

        result.Should().Be(25);
    }

    [Fact]
    public void GIVEN_PositiveRequestedLimit_WHEN_GettingEffectiveValue_THEN_ShouldReturnRequestedLimit()
    {
        var result = ResultLimit.GetEffectiveValue(requestedLimit: 7, defaultLimit: 25);

        result.Should().Be(7);
    }

    [Fact]
    public void GIVEN_ZeroRequestedLimit_WHEN_GettingEffectiveValue_THEN_ShouldReturnZero()
    {
        var result = ResultLimit.GetEffectiveValue(requestedLimit: 0, defaultLimit: 25);

        result.Should().Be(0);
    }

    [Fact]
    public void GIVEN_NegativeRequestedLimit_WHEN_GettingEffectiveValue_THEN_ShouldThrow()
    {
        var action = () => ResultLimit.GetEffectiveValue(requestedLimit: -1, defaultLimit: 25);

        action.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("requestedLimit");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void GIVEN_NonPositiveDefaultLimit_WHEN_GettingEffectiveValue_THEN_ShouldThrow(int defaultLimit)
    {
        var action = () => ResultLimit.GetEffectiveValue(requestedLimit: null, defaultLimit);

        action.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(defaultLimit));
    }
}
