using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.TestSupport;

using Xunit;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class ProjectFileUtilitiesTests
{
    [Fact]
    public async Task GIVEN_MissingSolutionPath_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var result = await ProjectFileUtilities.GetSolutionHierarchyAsync(null, TestContext.Current.CancellationToken);

        result.Should().Be(ProjectFileUtilities.SolutionHierarchyInfo.Empty);
    }

    [Fact]
    public async Task GIVEN_UnsupportedSolutionExtension_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.txt");
            await File.WriteAllTextAsync(solutionPath, "content", TestContext.Current.CancellationToken);

            var result = await ProjectFileUtilities.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.Should().Be(ProjectFileUtilities.SolutionHierarchyInfo.Empty);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_InvalidSolutionContent_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, "<Solution>", TestContext.Current.CancellationToken);

            var result = await ProjectFileUtilities.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.Should().Be(ProjectFileUtilities.SolutionHierarchyInfo.Empty);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_SlnHierarchy_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFoldersAndProjectMembership()
    {
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.sln");
            await File.WriteAllTextAsync(solutionPath, CreateSlnContent().Replace("\n", Environment.NewLine), TestContext.Current.CancellationToken);

            var result = await ProjectFileUtilities.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.Folders.Should().BeEquivalentTo(
            [
                new { Name = "src", Path = "src", ParentPath = (string?)null },
                new { Name = "core", Path = "src/core", ParentPath = (string?)"src" },
            ]);
            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Lib/Lib.csproj", "src/core"));
            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Root/Root.csproj", null));
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_SlnxHierarchy_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFoldersAndProjectMembership()
    {
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent().Replace("\n", Environment.NewLine), TestContext.Current.CancellationToken);

            var result = await ProjectFileUtilities.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.Folders.Should().BeEquivalentTo(
            [
                new { Name = "src", Path = "src", ParentPath = (string?)null },
                new { Name = "core", Path = "src/core", ParentPath = (string?)"src" },
            ]);
            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Lib/Lib.csproj", "src/core"));
            result.ProjectFolderPaths.Should().Contain(new KeyValuePair<string, string?>("Root/Root.csproj", null));
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_TargetFrameworksImportedFromProps_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEvaluatedValues()
    {
        MsBuildTestRegistration.EnsureRegistered();
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

            var result = ProjectFileUtilities.GetTargetFrameworks(projectPath);

            result.Should().Equal("net10.0", "net9.0");
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    private static string CreateDirectoryPath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-project-file-utilities-tests", Guid.NewGuid().ToString("n"));
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

    private static string CreateSlnContent()
    {
        return """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "core", "core", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Lib", "Lib\Lib.csproj", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Root", "Root\Root.csproj", "{44444444-4444-4444-4444-444444444444}"
            EndProject
            Global
            	GlobalSection(NestedProjects) = preSolution
            		{22222222-2222-2222-2222-222222222222} = {11111111-1111-1111-1111-111111111111}
            		{33333333-3333-3333-3333-333333333333} = {22222222-2222-2222-2222-222222222222}
            	EndGlobalSection
            EndGlobal
            """;
    }

    private static string CreateSlnxContent()
    {
        return """
            <Solution>
              <Folder Name="/src/" />
              <Folder Name="/src/core/">
                <Project Path="Lib/Lib.csproj" />
              </Folder>
              <Project Path="Root/Root.csproj" />
            </Solution>
            """;
    }
}
