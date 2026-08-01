namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class LocationSelectorValidatorTests
{
    private readonly LocationSelectorValidator _target = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void GIVEN_NotExactlyOneVariant_WHEN_Validating_THEN_ShouldReturnFailure(bool hasSpan, bool hasSelection)
    {
        var selector = new LocationSelector
        {
            Span = hasSpan ? new TextSpanSelector() : null,
            Selection = hasSelection ? new TextSelectionSelector() : null,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeFalse();
        result.Failures.Should().ContainSingle();
        result.Failures[0].MemberNames.Should().BeEquivalentTo(
            nameof(LocationSelector.Span),
            nameof(LocationSelector.Selection));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GIVEN_ExactlyOneVariant_WHEN_Validating_THEN_ShouldReturnNoFailures(bool hasSpan, bool hasSelection)
    {
        var selector = new LocationSelector
        {
            Span = hasSpan ? new TextSpanSelector() : null,
            Selection = hasSelection ? new TextSelectionSelector() : null,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }
}
