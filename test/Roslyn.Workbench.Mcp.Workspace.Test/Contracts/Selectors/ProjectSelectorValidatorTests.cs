namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class ProjectSelectorValidatorTests
{
    private readonly ProjectSelectorValidator _target = new();

    [Fact]
    public void GIVEN_NoMeaningfulQualifier_WHEN_Validating_THEN_ShouldReturnFailure()
    {
        var selector = new ProjectSelector
        {
            ProjectId = " ",
            Name = string.Empty,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeFalse();
        result.Failures.Should().ContainSingle();
        result.Failures[0].MemberNames.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("ProjectId")]
    [InlineData("Name")]
    [InlineData("Path")]
    [InlineData("TargetFramework")]
    [InlineData("All")]
    public void GIVEN_OneOrMoreMeaningfulQualifiers_WHEN_Validating_THEN_ShouldReturnNoFailures(string scenario)
    {
        var selector = new ProjectSelector
        {
            ProjectId = scenario is "ProjectId" or "All" ? "ProjectId" : null,
            Name = scenario is "Name" or "All" ? "Name" : null,
            Path = scenario is "Path" or "All" ? "Path" : null,
            TargetFramework = scenario is "TargetFramework" or "All" ? "TargetFramework" : null,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }
}
