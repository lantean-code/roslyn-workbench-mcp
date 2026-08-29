namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

public sealed class WorkspaceResolverFactoryTests : IDisposable
{
    private readonly Mock<IAddressableDocumentEligibility> _addressableDocumentEligibility;
    private readonly Mock<IWorkspaceSelectorFactory> _selectorFactory;
    private readonly AdhocWorkspace _workspace;
    private readonly WorkspaceResolverFactory _target;

    public WorkspaceResolverFactoryTests()
    {
        _addressableDocumentEligibility = new Mock<IAddressableDocumentEligibility>();
        _addressableDocumentEligibility
            .Setup(item => item.IsAddressable(It.IsAny<Document>()))
            .Returns(true);
        _selectorFactory = new Mock<IWorkspaceSelectorFactory>();
        _workspace = new AdhocWorkspace();
        var pathComparison = new Mock<IWorkspacePathComparison>();
        pathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);
        var workspacePathService = new Mock<IWorkspacePathService>();
        var pathServiceFactory = new Mock<IWorkspacePathServiceFactory>();
        pathServiceFactory
            .Setup(item => item.Create(It.IsAny<WorkspaceIdentity>()))
            .Returns(workspacePathService.Object);

        _target = new WorkspaceResolverFactory(
            _addressableDocumentEligibility.Object,
            pathComparison.Object,
            pathServiceFactory.Object,
            _selectorFactory.Object);
    }

    [Fact]
    public async Task GIVEN_SolutionAndSnapshotContext_WHEN_CreatingResolver_THEN_ShouldResolveAgainstSuppliedSolution()
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var document = _workspace.AddDocument(project.Id, "Document.cs", SourceText.From("class C { }"));
        var identity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 2,
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };
        var targetFrameworkMappings = new Dictionary<ProjectId, string>
        {
            [project.Id] = "net10.0",
        };
        var targetFrameworks = new WorkspaceProjectTargetFrameworkMap(targetFrameworkMappings);
        var expectedSelector = new CanonicalLocationSelector
        {
            Span = SelectorTestFactory.CreateTextSpanSelector(
                new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                start: 6,
                length: 1),
        };

        _selectorFactory
            .Setup(item => item.CreateCanonicalLocationSelector(It.IsAny<ResolvedLocation>()))
            .Returns(expectedSelector);

        var result = _target.Create(
            document.Project.Solution,
            identity,
            targetFrameworks,
            WorkspaceSnapshotTestFactory.CreatePrecondition(identity.WorkspaceId, identity.WorkspaceEpoch, transactionRevision: 3));
        var resolution = result.ResolveDocument(new DocumentSelector { DocumentId = document.Id.Id.ToString() });
        var projectResolution = result.ResolveProject(new ProjectSelector { TargetFramework = "NET10.0" });
        var syntaxTree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var location = syntaxTree!.GetLocation(new TextSpan(6, 1));
        var resolvedLocation = result.CreateResolvedLocation(location);

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().BeSameAs(document);
        projectResolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        projectResolution.Value!.Id.Should().Be(project.Id);
        resolvedLocation!.Selector!.Span.Should().BeSameAs(expectedSelector.Span);
        _selectorFactory.Verify(item => item.CreateCanonicalLocationSelector(It.IsAny<ResolvedLocation>()), Times.Once);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
