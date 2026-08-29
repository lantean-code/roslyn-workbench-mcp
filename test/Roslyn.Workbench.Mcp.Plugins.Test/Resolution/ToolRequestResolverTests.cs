using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Workbench.Mcp.Plugins.Resolution;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Resolution;

public sealed class ToolRequestResolverTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;

    public ToolRequestResolverTests()
    {
        _workspace = new AdhocWorkspace();
    }

    [Theory]
    [InlineData("Solution")]
    [InlineData("Project")]
    [InlineData("Projects")]
    public void GIVEN_ScopeContainsIneligibleDocument_WHEN_ResolvingDocuments_THEN_ShouldExcludeIt(string scopeKind)
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var includedDocument = _workspace.AddDocument(project.Id, "Included.cs", SourceText.From("class Included { }"));
        var excludedDocument = _workspace.AddDocument(project.Id, "Excluded.cs", SourceText.From("class Excluded { }"));
        var currentProject = _workspace.CurrentSolution.GetProject(project.Id)
            ?? throw new InvalidOperationException("The project was not found in the current solution.");
        var currentIncludedDocument = currentProject.GetDocument(includedDocument.Id)
            ?? throw new InvalidOperationException("The included document was not found in the current project.");
        var projectSelector = new ProjectSelector { Name = "Project" };
        var scope = CreateScope(scopeKind, projectSelector);
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        workspaceResolver
            .Setup(item => item.ResolveProject(projectSelector))
            .Returns(SelectorResolveResult.Resolved(currentProject));
        workspaceResolver
            .Setup(item => item.GetDocuments(It.IsAny<Solution>()))
            .Returns([currentIncludedDocument]);
        workspaceResolver
            .Setup(item => item.GetDocuments(currentProject))
            .Returns([currentIncludedDocument]);
        var context = new Mock<IToolExecutionContext>();
        context.SetupGet(item => item.CurrentSolution).Returns(_workspace.CurrentSolution);
        context.SetupGet(item => item.WorkspaceResolver).Returns(workspaceResolver.Object);
        var target = new ToolRequestResolver();

        var result = target.ResolveDocuments<object>(scope, context.Object);

        result.Value.Should().Equal(currentIncludedDocument);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private static ScopeSelector CreateScope(string scopeKind, ProjectSelector projectSelector)
    {
        return scopeKind switch
        {
            "Solution" => new ScopeSelector { Kind = ScopeKind.Solution },
            "Project" => new ScopeSelector { Kind = ScopeKind.Project, Project = projectSelector },
            "Projects" => new ScopeSelector { Kind = ScopeKind.Projects, Projects = [projectSelector] },
            _ => throw new InvalidOperationException("Unsupported scope kind."),
        };
    }
}
