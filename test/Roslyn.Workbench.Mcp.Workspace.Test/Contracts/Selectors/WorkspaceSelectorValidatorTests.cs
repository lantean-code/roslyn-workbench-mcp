namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class WorkspaceSelectorValidatorTests
{
    private readonly WorkspaceSelectorValidator _target = new();

    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    public void GIVEN_NoMeaningfulIdentity_WHEN_Validating_THEN_ShouldReturnFailure(string? alias, string? path)
    {
        var selector = new WorkspaceSelector
        {
            Alias = alias,
            Path = path,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeFalse();
        result.Failures.Should().ContainSingle();
        result.Failures[0].MemberNames.Should().BeEquivalentTo(
            nameof(WorkspaceSelector.WorkspaceId),
            nameof(WorkspaceSelector.Alias),
            nameof(WorkspaceSelector.Path));
    }

    [Theory]
    [InlineData("WorkspaceId")]
    [InlineData("Alias")]
    [InlineData("Path")]
    [InlineData("All")]
    public void GIVEN_OneOrMoreMeaningfulIdentities_WHEN_Validating_THEN_ShouldReturnNoFailures(string scenario)
    {
        var selector = new WorkspaceSelector
        {
            WorkspaceId = scenario is "WorkspaceId" or "All" ? Guid.Parse("11111111-1111-1111-1111-111111111111") : null,
            Alias = scenario is "Alias" or "All" ? "Alias" : null,
            Path = scenario is "Path" or "All" ? "Path" : null,
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }
}
