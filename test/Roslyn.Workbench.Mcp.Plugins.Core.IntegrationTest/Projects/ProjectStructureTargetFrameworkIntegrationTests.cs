using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Projects;

public sealed class ProjectStructureTargetFrameworkIntegrationTests
{
    [Fact]
    public void GIVEN_TargetFrameworksImportedFromProps_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEvaluatedValues()
    {
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var buildDirectoryPath = Path.Combine(directoryPath, "build");
            Directory.CreateDirectory(buildDirectoryPath);

            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            var importedPropsPath = Path.Combine(buildDirectoryPath, "Frameworks.props");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="build\Frameworks.props" />
                </Project>
                """);
            File.WriteAllText(importedPropsPath, """
                <Project>
                  <PropertyGroup>
                    <TargetFrameworks>net10.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                </Project>
                """);

            var result = target.GetTargetFrameworks(projectPath);

            result.IsSucceeded.Should().BeTrue();
            result.TargetFrameworks.Should().Equal("net10.0", "net9.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }


    [Fact]
    public void GIVEN_MissingProjectFile_WHEN_GettingTargetFrameworks_THEN_ShouldReturnFailure()
    {
        var target = new ProjectStructureService();
        var projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "Missing.csproj");

        var result = target.GetTargetFrameworks(projectPath);

        result.IsSucceeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain(projectPath);
    }

    [Fact]
    public void GIVEN_MalformedProject_WHEN_GettingTargetFrameworks_THEN_ShouldReturnFailure()
    {
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Malformed.csproj");
            File.WriteAllText(projectPath, "<Project><PropertyGroup>");

            var result = target.GetTargetFrameworks(projectPath);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain(projectPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_ProjectWithoutTargetFramework_WHEN_GettingTargetFrameworks_THEN_ShouldReturnSuccessfulEmptyResult()
    {
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            File.WriteAllText(projectPath, "<Project />");

            var result = target.GetTargetFrameworks(projectPath);

            result.IsSucceeded.Should().BeTrue();
            result.TargetFrameworks.Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_ProjectWithSingleTargetFramework_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEvaluatedValue()
    {
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            File.WriteAllText(projectPath, "<Project><PropertyGroup><TargetFramework> net10.0 </TargetFramework></PropertyGroup></Project>");

            var result = target.GetTargetFrameworks(projectPath);

            result.IsSucceeded.Should().BeTrue();
            result.TargetFrameworks.Should().ContainSingle().Which.Should().Be("net10.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_ProjectWithoutFilePath_WHEN_GettingTargetFrameworks_THEN_ShouldReturnSuccessfulEmptyResult()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Project", LanguageNames.CSharp);
        var target = new ProjectStructureService();

        var result = target.GetTargetFrameworks(project);

        result.IsSucceeded.Should().BeTrue();
        result.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ProjectBatchContainsDuplicateAndMissingPaths_WHEN_GettingTargetFrameworks_THEN_ShouldPreserveInputOrder()
    {
        using var workspace = new AdhocWorkspace();
        var target = new ProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var sharedProjectPath = Path.Combine(directoryPath, "Shared.csproj");
            var otherProjectPath = Path.Combine(directoryPath, "Other.csproj");
            File.WriteAllText(sharedProjectPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            File.WriteAllText(otherProjectPath, "<Project><PropertyGroup><TargetFrameworks>net9.0;net8.0</TargetFrameworks></PropertyGroup></Project>");

            var sharedProjectA = AddProject(workspace, "SharedA", sharedProjectPath);
            var pathlessProject = AddProject(workspace, "Pathless", filePath: null);
            var sharedProjectB = AddProject(workspace, "SharedB", sharedProjectPath);
            var otherProject = AddProject(workspace, "Other", otherProjectPath);

            var results = target.GetTargetFrameworks(
            [
                sharedProjectA,
                pathlessProject,
                sharedProjectB,
                otherProject,
            ]);

            results.Should().HaveCount(4);
            results[0].TargetFrameworks.Should().Equal("net10.0");
            results[1].TargetFrameworks.Should().BeEmpty();
            results[2].Should().BeSameAs(results[0]);
            results[3].TargetFrameworks.Should().Equal("net9.0", "net8.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    private static Project AddProject(AdhocWorkspace workspace, string name, string? filePath)
    {
        var projectInfo = Microsoft.CodeAnalysis.ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            LanguageNames.CSharp,
            filePath: filePath);

        return workspace.AddProject(projectInfo);
    }

    private static string CreateDirectoryPath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-project-structure-service-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
