using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceProjectTargetFrameworkCollectorTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;

    public WorkspaceProjectTargetFrameworkCollectorTests()
    {
        _workspace = new AdhocWorkspace();
    }

    [Fact]
    public void GIVEN_ProjectWithoutFilePath_WHEN_CreatingMap_THEN_ShouldIgnoreProject()
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var pathComparison = new Mock<IWorkspacePathComparison>();
        var target = new WorkspaceProjectTargetFrameworkCollector(pathComparison.Object);

        var result = target.CreateMap(project.Solution);

        result.Matches(project.Id, "net10.0").Should().BeFalse();
        pathComparison.Verify(item => item.CreateKey(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ProjectWithoutReportedTargetFramework_WHEN_CreatingMap_THEN_ShouldLeaveProjectUnmapped()
    {
        var projectPath = Path.Combine("Workspace", "Project.csproj");
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath);
        var solution = _workspace.CurrentSolution.AddProject(projectInfo);
        var project = solution.Projects.Single();
        var projectPathKey = new FileSystemPathKey(projectPath, isCaseSensitive: true);
        var pathComparison = new Mock<IWorkspacePathComparison>();
        pathComparison.Setup(item => item.CreateKey(projectPath)).Returns(projectPathKey);
        var target = new WorkspaceProjectTargetFrameworkCollector(pathComparison.Object);

        var result = target.CreateMap(solution);

        result.Matches(project.Id, "net10.0").Should().BeFalse();
        pathComparison.Verify(item => item.CreateKey(projectPath), Times.Once);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
