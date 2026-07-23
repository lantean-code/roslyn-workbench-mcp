using Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Projects.Caching;

public sealed class ProjectTargetFrameworkCacheKeyTests
{
    [Fact]
    public void GIVEN_EquivalentInputs_WHEN_ComparingKeys_THEN_ShouldBeEqual()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var first = new ProjectTargetFrameworkCacheKey(solution.Solution, project.FilePath!);
        var second = new ProjectTargetFrameworkCacheKey(solution.Solution, project.FilePath!);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void GIVEN_DifferentSolutionSnapshot_WHEN_ComparingKeys_THEN_ShouldNotBeEqual()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var first = new ProjectTargetFrameworkCacheKey(solution.Solution, project.FilePath!);
        var changedSolution = solution.Solution.AddDocument(DocumentId.CreateNewId(project.Id), "Other.cs", "class Other { }");
        var second = new ProjectTargetFrameworkCacheKey(changedSolution, project.FilePath!);

        first.Should().NotBe(second);
    }

    [Fact]
    public void GIVEN_DifferentProjectPath_WHEN_ComparingKeys_THEN_ShouldNotBeEqual()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var first = new ProjectTargetFrameworkCacheKey(solution.Solution, project.FilePath!);
        var second = new ProjectTargetFrameworkCacheKey(solution.Solution, "Other.csproj");

        first.Should().NotBe(second);
    }

    private static InMemoryRoslynSolution CreateSolution()
    {
        return RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Project.cs",
                        Source = "class Project { }",
                    },
                ],
            },
        ]);
    }
}
