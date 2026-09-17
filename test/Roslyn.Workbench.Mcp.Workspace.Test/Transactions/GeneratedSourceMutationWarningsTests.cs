namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class GeneratedSourceMutationWarningsTests
{
    [Fact]
    public void GIVEN_NoGeneratedPaths_WHEN_CreatingWarnings_THEN_ShouldReturnEmptyCollection()
    {
        var result = GeneratedSourceMutationWarnings.Create([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_MoreThanFiveGeneratedPaths_WHEN_CreatingWarnings_THEN_ShouldBoundDisplayedPaths()
    {
        var paths = Enumerable.Range(1, 7).Select(index => $"Generated{index}.g.cs").ToArray();

        var result = GeneratedSourceMutationWarnings.Create(paths);

        result.Should().ContainSingle();
        result[0].Code.Should().Be(GeneratedSourceMutationWarnings.WarningCode);
        result[0].Message.Should().Contain("'Generated5.g.cs'");
        result[0].Message.Should().Contain("and 2 more");
        result[0].Message.Should().NotContain("Generated6.g.cs");
    }

    [Fact]
    public void GIVEN_FiveGeneratedPaths_WHEN_CreatingWarnings_THEN_ShouldNotReportOmittedPaths()
    {
        var paths = Enumerable.Range(1, 5).Select(index => $"Generated{index}.g.cs").ToArray();

        var result = GeneratedSourceMutationWarnings.Create(paths);

        result.Should().ContainSingle();
        result[0].Message.Should().NotContain("more");
    }
}
