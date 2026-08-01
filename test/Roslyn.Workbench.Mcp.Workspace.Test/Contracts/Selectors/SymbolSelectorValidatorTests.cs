namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class SymbolSelectorValidatorTests
{
    private readonly SymbolSelectorValidator _target = new();

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, " ")]
    [InlineData(true, "DocumentationCommentId")]
    public void GIVEN_NotExactlyOneIdentity_WHEN_Validating_THEN_ShouldReturnFailure(
        bool hasLocation,
        string? documentationCommentId)
    {
        var selector = new SymbolSelector
        {
            Location = hasLocation ? new LocationSelector() : null,
            DocumentationCommentId = documentationCommentId,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeFalse();
        result.Failures.Should().ContainSingle();
        result.Failures[0].MemberNames.Should().BeEquivalentTo(
            nameof(SymbolSelector.Location),
            nameof(SymbolSelector.DocumentationCommentId));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, " ")]
    [InlineData(false, "DocumentationCommentId")]
    public void GIVEN_ExactlyOneIdentity_WHEN_Validating_THEN_ShouldReturnNoFailures(
        bool hasLocation,
        string? documentationCommentId)
    {
        var selector = new SymbolSelector
        {
            Location = hasLocation ? new LocationSelector() : null,
            DocumentationCommentId = documentationCommentId,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }
}
