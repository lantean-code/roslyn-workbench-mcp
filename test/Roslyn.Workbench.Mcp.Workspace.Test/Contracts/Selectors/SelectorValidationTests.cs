namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class SelectorValidationTests
{
    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(0, 10, 10, true)]
    [InlineData(10, 0, 10, true)]
    [InlineData(-1, 1, 10, false)]
    [InlineData(0, -1, 10, false)]
    [InlineData(10, 1, 10, false)]
    [InlineData(int.MaxValue, 1, 10, false)]
    public void GIVEN_TextSpanAndDocumentLength_WHEN_CheckingContainment_THEN_ShouldReturnExpectedResult(
        int start,
        int length,
        int documentLength,
        bool expected)
    {
        var selector = new TextSpanSelector
        {
            Start = start,
            Length = length,
        };

        var result = WorkspaceContractValidator.IsWithinDocument(selector, documentLength);

        result.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new DocumentSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithBothSelectors_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new DocumentSelector
        {
            Path = "Path",
            DocumentId = "DocumentId",
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithPath_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var selector = new DocumentSelector
        {
            Path = "Path",
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithDocumentId_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var errors = WorkspaceContractValidator.Validate(new DocumentSelector { DocumentId = "DocumentId" });

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithValidProject_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var selector = new DocumentSelector
        {
            Path = "Path",
            Project = new ProjectSelector
            {
                ProjectId = "ProjectId",
            },
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithEmptyProject_WHEN_Validated_THEN_ShouldReturnProjectValidationError()
    {
        var selector = new DocumentSelector
        {
            Path = "Path",
            Project = new ProjectSelector(),
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("ProjectSelector", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_ProjectSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new ProjectSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("at least one"));
    }

    [Theory]
    [InlineData("ProjectId")]
    [InlineData("Name")]
    [InlineData("Path")]
    [InlineData("TargetFramework")]
    public void GIVEN_ProjectSelectorValue_WHEN_Validated_THEN_ShouldReturnNoValidationErrors(string field)
    {
        var selector = new ProjectSelector
        {
            ProjectId = field == "ProjectId" ? "ProjectId" : null,
            Name = field == "Name" ? "Name" : null,
            Path = field == "Path" ? "Path" : null,
            TargetFramework = field == "TargetFramework" ? "TargetFramework" : null,
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_WorkspaceSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new WorkspaceSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("at least one"));
    }

    [Fact]
    public void GIVEN_WorkspaceSelectorWithWorkspaceId_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var selector = new WorkspaceSelector
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Alias")]
    [InlineData("Path")]
    public void GIVEN_WorkspaceSelectorAlternative_WHEN_Validated_THEN_ShouldReturnNoValidationErrors(string field)
    {
        var selector = new WorkspaceSelector
        {
            Alias = field == "Alias" ? "Alias" : null,
            Path = field == "Path" ? "Path" : null,
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_LocationSelectorWithoutVariant_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var errors = WorkspaceContractValidator.Validate(new LocationSelector());

        errors.Should().ContainSingle();
    }

    [Theory]
    [InlineData("Span")]
    [InlineData("Selection")]
    public void GIVEN_LocationSelectorVariant_WHEN_Validated_THEN_ShouldReturnNoValidationErrors(string variant)
    {
        var selector = new LocationSelector
        {
            Span = variant == "Span" ? new TextSpanSelector() : null,
            Selection = variant == "Selection" ? new TextSelectionSelector() : null,
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_LocationSelectorWithBothVariants_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = "Path",
                },
                Start = 1,
                Length = 1,
            },
            Selection = new TextSelectionSelector
            {
                Document = new DocumentSelector
                {
                    Path = "Path",
                },
                SelectedText = "SelectedText",
            },
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_ScopeSelectorWithMismatchedKind_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new ScopeSelector
        {
            Kind = ScopeKind.Document,
            Project = new ProjectSelector
            {
                Name = "Name",
            },
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().Contain(error => error.Contains("Kind"));
    }

    [Theory]
    [InlineData("SolutionValid", 0)]
    [InlineData("SolutionProject", 1)]
    [InlineData("SolutionDocument", 1)]
    [InlineData("SolutionProjects", 1)]
    [InlineData("ProjectValid", 0)]
    [InlineData("ProjectMissing", 1)]
    [InlineData("ProjectDocument", 1)]
    [InlineData("ProjectProjects", 1)]
    [InlineData("DocumentValid", 0)]
    [InlineData("DocumentMissing", 1)]
    [InlineData("DocumentProject", 1)]
    [InlineData("DocumentProjects", 1)]
    [InlineData("ProjectsValid", 0)]
    [InlineData("ProjectsNull", 1)]
    [InlineData("ProjectsEmpty", 1)]
    [InlineData("ProjectsProject", 1)]
    [InlineData("ProjectsDocument", 1)]
    public void GIVEN_ScopeSelectorShape_WHEN_Validated_THEN_ShouldReturnExpectedErrors(
        string scenario,
        int expectedErrorCount)
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
            "ProjectDocument" => new ScopeSelector { Kind = ScopeKind.Project, Project = project, Document = document },
            "ProjectProjects" => new ScopeSelector { Kind = ScopeKind.Project, Project = project, Projects = [project] },
            "DocumentValid" => new ScopeSelector { Kind = ScopeKind.Document, Document = document },
            "DocumentMissing" => new ScopeSelector { Kind = ScopeKind.Document },
            "DocumentProject" => new ScopeSelector { Kind = ScopeKind.Document, Document = document, Project = project },
            "DocumentProjects" => new ScopeSelector { Kind = ScopeKind.Document, Document = document, Projects = [project] },
            "ProjectsValid" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [project] },
            "ProjectsNull" => new ScopeSelector { Kind = ScopeKind.Projects },
            "ProjectsEmpty" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [] },
            "ProjectsProject" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [project], Project = project },
            "ProjectsDocument" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [project], Document = document },
            _ => throw new InvalidOperationException("Unsupported scope scenario."),
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().HaveCount(expectedErrorCount);
    }

    [Fact]
    public void GIVEN_SymbolSelectorWithoutAnyResolver_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new SymbolSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_SymbolSelectorWithBothResolvers_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Path",
                    },
                    Start = 0,
                    Length = 1,
                },
            },
            DocumentationCommentId = "M:Sample.Type.Method",
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Theory]
    [InlineData("Location")]
    [InlineData("DocumentationCommentId")]
    public void GIVEN_SymbolSelectorResolver_WHEN_Validated_THEN_ShouldReturnNoValidationErrors(string resolver)
    {
        var selector = new SymbolSelector
        {
            Location = resolver == "Location" ? new LocationSelector() : null,
            DocumentationCommentId = resolver == "DocumentationCommentId" ? "DocumentationCommentId" : null,
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_SymbolSelectorWithEmptyProjectScope_WHEN_Validated_THEN_ShouldReturnProjectValidationError()
    {
        var selector = new SymbolSelector
        {
            DocumentationCommentId = "T:Sample.Type",
            Project = new ProjectSelector(),
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("ProjectSelector"));
    }

    [Fact]
    public void GIVEN_SymbolSelectorWithValidProjectScope_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var selector = new SymbolSelector
        {
            DocumentationCommentId = "T:Sample.Type",
            Project = new ProjectSelector { Name = "Sample" },
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }
}
