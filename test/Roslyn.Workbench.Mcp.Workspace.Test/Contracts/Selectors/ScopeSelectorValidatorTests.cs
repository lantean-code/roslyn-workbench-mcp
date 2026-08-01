namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class ScopeSelectorValidatorTests
{
    private readonly ScopeSelectorValidator _target = new();

    [Theory]
    [InlineData("SolutionValid", 0)]
    [InlineData("SolutionProject", 1)]
    [InlineData("SolutionDocument", 1)]
    [InlineData("SolutionProjects", 1)]
    [InlineData("ProjectValid", 0)]
    [InlineData("ProjectMissing", 1)]
    [InlineData("ProjectForbidden", 1)]
    [InlineData("ProjectMissingAndForbidden", 2)]
    [InlineData("DocumentValid", 0)]
    [InlineData("DocumentMissing", 1)]
    [InlineData("DocumentForbidden", 1)]
    [InlineData("DocumentMissingAndForbidden", 2)]
    [InlineData("ProjectsValid", 0)]
    [InlineData("ProjectsMissing", 1)]
    [InlineData("ProjectsEmpty", 1)]
    [InlineData("ProjectsForbidden", 1)]
    [InlineData("ProjectsMissingAndForbidden", 2)]
    public void GIVEN_ScopeShape_WHEN_Validating_THEN_ShouldReturnExpectedFailures(
        string scenario,
        int expectedFailureCount)
    {
        var project = new ProjectSelector { Name = "Name" };
        var document = new DocumentSelector { Path = "Path" };
        var selector = scenario switch
        {
            "SolutionValid" => new ScopeSelector { Kind = ScopeKind.Solution },
            "SolutionProject" => new ScopeSelector { Kind = ScopeKind.Solution, Project = project },
            "SolutionDocument" => new ScopeSelector { Kind = ScopeKind.Solution, Document = document },
            "SolutionProjects" => new ScopeSelector { Kind = ScopeKind.Solution, Projects = [project] },
            "ProjectValid" => new ScopeSelector { Kind = ScopeKind.Project, Project = project },
            "ProjectMissing" => new ScopeSelector { Kind = ScopeKind.Project },
            "ProjectForbidden" => new ScopeSelector { Kind = ScopeKind.Project, Project = project, Document = document },
            "ProjectMissingAndForbidden" => new ScopeSelector { Kind = ScopeKind.Project, Projects = [project] },
            "DocumentValid" => new ScopeSelector { Kind = ScopeKind.Document, Document = document },
            "DocumentMissing" => new ScopeSelector { Kind = ScopeKind.Document },
            "DocumentForbidden" => new ScopeSelector { Kind = ScopeKind.Document, Document = document, Project = project },
            "DocumentMissingAndForbidden" => new ScopeSelector { Kind = ScopeKind.Document, Projects = [project] },
            "ProjectsValid" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [project] },
            "ProjectsMissing" => new ScopeSelector { Kind = ScopeKind.Projects },
            "ProjectsEmpty" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [] },
            "ProjectsForbidden" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [project], Document = document },
            "ProjectsMissingAndForbidden" => new ScopeSelector { Kind = ScopeKind.Projects, Project = project },
            _ => throw new InvalidOperationException("Unsupported scope scenario."),
        };

        var result = _target.Validate(selector);

        result.IsValid.Should().Be(expectedFailureCount == 0);
        result.Failures.Should().HaveCount(expectedFailureCount);
    }
}
