namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

public sealed class WorkspaceResolverFactoryTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly WorkspaceResolverFactory _target;

    public WorkspaceResolverFactoryTests()
    {
        _workspace = new AdhocWorkspace();
        _target = new WorkspaceResolverFactory();
    }

    [Fact]
    public void GIVEN_SolutionAndSnapshotContext_WHEN_CreatingResolver_THEN_ShouldResolveAgainstSuppliedSolution()
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var document = _workspace.AddDocument(project.Id, "Document.cs", SourceText.From("class C { }"));
        var identity = new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 2,
            LoadedPath = "LoadedPath",
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
