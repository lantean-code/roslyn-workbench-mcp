namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class DocumentSelectorValidatorTests
{
    private readonly DocumentSelectorValidator _target = new();

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", "")]
    [InlineData("Path", "DocumentId")]
    public void GIVEN_NotExactlyOneIdentity_WHEN_Validating_THEN_ShouldReturnFailure(string? path, string? documentId)
    {
        var selector = new DocumentSelector
        {
            Path = path,
            DocumentId = documentId,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeFalse();
        result.Failures.Should().ContainSingle();
        result.Failures[0].MemberNames.Should().BeEquivalentTo(
            nameof(DocumentSelector.Path),
            nameof(DocumentSelector.DocumentId));
    }

    [Theory]
    [InlineData("Path", null)]
    [InlineData(null, "DocumentId")]
    [InlineData(" ", "DocumentId")]
    [InlineData("Path", " ")]
    public void GIVEN_ExactlyOneIdentity_WHEN_Validating_THEN_ShouldReturnNoFailures(string? path, string? documentId)
    {
        var selector = new DocumentSelector
        {
            Path = path,
            DocumentId = documentId,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }
}
