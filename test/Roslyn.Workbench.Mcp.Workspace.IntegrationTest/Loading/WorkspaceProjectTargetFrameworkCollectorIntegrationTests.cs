using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceProjectTargetFrameworkCollectorIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_MultiTargetProjectNamesContradictLoadMetadata_WHEN_CreatingMap_THEN_ShouldLeaveProjectsUnmapped()
    {
        MsBuildTestRegistration.EnsureRegistered();
        using var fixture = WorkspaceAssetMaterializer.Materialize("MultiTargetLinked");
        var projectPath = Path.Combine(fixture.WorkspaceRoot, "MultiTarget", "MultiTarget.csproj");
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var target = new WorkspaceProjectTargetFrameworkCollector(pathComparison);
        using var workspace = MSBuildWorkspace.Create();

        var loadedProject = await workspace.OpenProjectAsync(
            projectPath,
            progress: target,
            cancellationToken: TestContext.Current.CancellationToken);

        var solution = loadedProject.Solution;
        foreach (var projectId in solution.ProjectIds)
        {
            solution = solution.WithProjectName(projectId, "UnexpectedProjectName");
        }

        var result = target.CreateMap(solution);

        foreach (var projectId in solution.ProjectIds)
        {
            result.Matches(projectId, "net10.0").Should().BeFalse();
            result.Matches(projectId, "netstandard2.0").Should().BeFalse();
        }
    }
}
