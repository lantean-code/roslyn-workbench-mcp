namespace Roslyn.Workbench.Mcp.CodeActions.Test.Resolution.Requests;

public sealed class CodeActionScopeResolverTests
{
    private readonly CodeActionScopeResolver _target;

    public CodeActionScopeResolverTests()
    {
        _target = new CodeActionScopeResolver();
    }

    [Fact]
    public void GIVEN_SolutionScope_WHEN_ResolvingScope_THEN_ShouldReturnEveryDocument()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var workspaceResolver = CreateWorkspaceResolver();

        var result = _target.Resolve(
            new ScopeSelector { Kind = ScopeKind.Solution },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Documents.Should().BeEquivalentTo(roslyn.Solution.Projects.SelectMany(static project => project.Documents));
        result.Projects.Should().BeEmpty();
        result.HasRejection.Should().BeFalse();
        workspaceResolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
        workspaceResolver.Verify(item => item.ResolveProject(It.IsAny<ProjectSelector>()), Times.Never);
    }

    [Fact]
    public void GIVEN_SolutionContainsIneligibleDocument_WHEN_ResolvingScope_THEN_ShouldExcludeIt()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var excludedDocument = roslyn.Solution.Projects.First().Documents.First();
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.GetDocuments(roslyn.Solution))
            .Returns(roslyn.Solution.Projects
                .SelectMany(static project => project.Documents)
                .Where(document => document.Id != excludedDocument.Id)
                .ToArray());

        var result = _target.Resolve(
            new ScopeSelector { Kind = ScopeKind.Solution },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Documents.Should().NotContain(excludedDocument);
        result.Documents.Should().HaveCount(roslyn.Solution.Projects.Sum(static project => project.DocumentIds.Count) - 1);
    }

    [Fact]
    public void GIVEN_DocumentScopeWithoutSelector_WHEN_ResolvingScope_THEN_ShouldRejectRequest()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceResolver = CreateWorkspaceResolver();

        var result = _target.Resolve(
            new ScopeSelector { Kind = ScopeKind.Document },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Rejection!.Error!.Code.Should().Be("InvalidRequest");
        workspaceResolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "DocumentNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "DocumentAmbiguous")]
    public void GIVEN_DocumentDoesNotResolve_WHEN_ResolvingScope_THEN_ShouldMapResolutionStatus(
        SelectorResolveStatus status,
        string expectedCode)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new DocumentSelector { Path = "Path" };
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.ResolveDocument(selector))
            .Returns(SelectorTestFactory.CreateUnresolvedResult<Document>(status));

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = selector,
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Rejection!.Error!.Code.Should().Be(expectedCode);
        workspaceResolver.Verify(item => item.ResolveDocument(selector), Times.Once);
    }

    [Fact]
    public void GIVEN_ResolvedDocument_WHEN_ResolvingScope_THEN_ShouldReturnDocument()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new DocumentSelector { Path = "Path" };
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.ResolveDocument(selector))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document));

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = selector,
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Documents.Should().Equal(roslyn.Document);
        result.Projects.Should().BeEmpty();
        result.HasRejection.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ProjectScopeWithoutSelector_WHEN_ResolvingScope_THEN_ShouldRejectRequest()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceResolver = CreateWorkspaceResolver();

        var result = _target.Resolve(
            new ScopeSelector { Kind = ScopeKind.Project },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Rejection!.Error!.Code.Should().Be("InvalidRequest");
        workspaceResolver.Verify(item => item.ResolveProject(It.IsAny<ProjectSelector>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "ProjectNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "ProjectAmbiguous")]
    public void GIVEN_ProjectDoesNotResolve_WHEN_ResolvingScope_THEN_ShouldMapResolutionStatus(
        SelectorResolveStatus status,
        string expectedCode)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new ProjectSelector { Name = "Name" };
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.ResolveProject(selector))
            .Returns(SelectorTestFactory.CreateUnresolvedResult<Project>(status));

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = selector,
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Rejection!.Error!.Code.Should().Be(expectedCode);
        workspaceResolver.Verify(item => item.ResolveProject(selector), Times.Once);
    }

    [Fact]
    public void GIVEN_ResolvedProjectHasExcludedDocument_WHEN_ResolvingScope_THEN_ShouldReturnFilteredDocuments()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new ProjectSelector { Name = "Name" };
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.ResolveProject(selector))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document.Project));
        workspaceResolver
            .Setup(item => item.GetDocuments(roslyn.Document.Project))
            .Returns([]);

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = selector,
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Documents.Should().BeEmpty();
        result.Projects.Should().Equal(roslyn.Document.Project);
        result.HasRejection.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_ProjectSetIsMissing_WHEN_ResolvingScope_THEN_ShouldRejectRequest(bool isNull)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceResolver = CreateWorkspaceResolver();

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Projects,
                Projects = isNull ? null : [],
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Rejection!.Error!.Code.Should().Be("InvalidRequest");
        workspaceResolver.Verify(item => item.ResolveProject(It.IsAny<ProjectSelector>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "ProjectNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "ProjectAmbiguous")]
    public void GIVEN_ProjectInSetDoesNotResolve_WHEN_ResolvingScope_THEN_ShouldMapResolutionStatus(
        SelectorResolveStatus status,
        string expectedCode)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new ProjectSelector { Name = "Name" };
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.ResolveProject(selector))
            .Returns(SelectorTestFactory.CreateUnresolvedResult<Project>(status));

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Projects,
                Projects = [selector],
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Rejection!.Error!.Code.Should().Be(expectedCode);
        workspaceResolver.Verify(item => item.ResolveProject(selector), Times.Once);
    }

    [Fact]
    public void GIVEN_DuplicateProjects_WHEN_ResolvingScope_THEN_ShouldReturnEachProjectOnce()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new ProjectSelector { Name = "Name" };
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.ResolveProject(selector))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document.Project));

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Projects,
                Projects = [selector, selector],
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Documents.Should().Equal(roslyn.Document);
        result.Projects.Should().Equal(roslyn.Document.Project);
        workspaceResolver.Verify(item => item.ResolveProject(selector), Times.Exactly(2));
    }

    [Fact]
    public void GIVEN_MultipleProjectsIncludeExcludedDocument_WHEN_ResolvingScope_THEN_ShouldReturnEveryProjectAndFilteredDocuments()
    {
        using var roslyn = CodeActionExecutionTestFactory.CreateTwoProjectSolution();
        var firstProject = roslyn.Solution.Projects.Single(project => project.Name == "FirstProject");
        var secondProject = roslyn.Solution.Projects.Single(project => project.Name == "SecondProject");
        var firstSelector = new ProjectSelector { Name = "FirstProject" };
        var secondSelector = new ProjectSelector { Name = "SecondProject" };
        var workspaceResolver = CreateWorkspaceResolver();
        workspaceResolver
            .Setup(item => item.ResolveProject(firstSelector))
            .Returns(SelectorResolveResult.Resolved(firstProject));

        workspaceResolver
            .Setup(item => item.ResolveProject(secondSelector))
            .Returns(SelectorResolveResult.Resolved(secondProject));
        workspaceResolver
            .Setup(item => item.GetDocuments(firstProject))
            .Returns([]);

        var result = _target.Resolve(
            new ScopeSelector
            {
                Kind = ScopeKind.Projects,
                Projects = [firstSelector, secondSelector],
            },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Projects.Should().Equal(firstProject, secondProject);
        result.Documents.Should().BeEquivalentTo(secondProject.Documents);
        result.Documents.Should().NotContain(firstProject.Documents.Single());
    }

    [Fact]
    public void GIVEN_UnsupportedScope_WHEN_ResolvingScope_THEN_ShouldRejectRequest()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workspaceResolver = CreateWorkspaceResolver();

        var result = _target.Resolve(
            new ScopeSelector { Kind = (ScopeKind)int.MaxValue },
            roslyn.Solution,
            workspaceResolver.Object);

        result.Rejection!.Error!.Code.Should().Be("InvalidRequest");
    }

    private static Mock<IWorkspaceResolver> CreateWorkspaceResolver()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        workspaceResolver
            .Setup(item => item.GetDocuments(It.IsAny<Solution>()))
            .Returns((Solution solution) => solution.Projects.SelectMany(static project => project.Documents).ToArray());
        workspaceResolver
            .Setup(item => item.GetDocuments(It.IsAny<Project>()))
            .Returns((Project project) => project.Documents.ToArray());

        return workspaceResolver;
    }
}
