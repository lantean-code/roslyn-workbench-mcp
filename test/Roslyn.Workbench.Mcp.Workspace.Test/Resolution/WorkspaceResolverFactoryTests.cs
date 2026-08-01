namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

public sealed class WorkspaceResolverFactoryTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly WorkspaceResolverFactory _target;

    public WorkspaceResolverFactoryTests()
    {
        _workspace = new AdhocWorkspace();
        var pathComparison = new Mock<IWorkspacePathComparison>();
        pathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);

        _target = new WorkspaceResolverFactory(pathComparison.Object);
    }

    [Fact]
    public void GIVEN_SolutionAndSnapshotContext_WHEN_CreatingResolver_THEN_ShouldResolveAgainstSuppliedSolution()
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

        var result = _target.Create(document.Project.Solution, identity, 3);
        var resolution = result.ResolveDocument(new DocumentSelector { DocumentId = document.Id.Id.ToString() });

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().BeSameAs(document);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
