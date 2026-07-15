using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class DefaultProjectStructureServiceIntegrationTests
{
    [Fact]
    public async Task GIVEN_MissingSolutionPath_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var target = new DefaultProjectStructureService();

        var result = await target.GetSolutionHierarchyAsync(null, TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        result.Folders.Should().BeEmpty();
        result.ProjectFolderPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnsupportedSolutionExtension_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnEmpty()
    {
        var target = new DefaultProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.txt");
            await File.WriteAllTextAsync(solutionPath, "content", TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
            result.Folders.Should().BeEmpty();
            result.ProjectFolderPaths.Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_InvalidSolutionContent_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure()
    {
        var target = new DefaultProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, "<Solution>", TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain(solutionPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_SlnHierarchy_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFoldersAndProjectMembership()
    {
        var target = new DefaultProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.sln");
            await File.WriteAllTextAsync(solutionPath, CreateSlnContent().Replace("\n", Environment.NewLine), TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
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
        var target = new DefaultProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent().Replace("\n", Environment.NewLine), TestContext.Current.CancellationToken);

            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeTrue();
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
        var target = new DefaultProjectStructureService();
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
    public async Task GIVEN_MissingSolutionFile_WHEN_GettingSolutionHierarchy_THEN_ShouldReturnFailure()
    {
        var target = new DefaultProjectStructureService();
        var directoryPath = CreateDirectoryPath();
        var solutionPath = Path.Combine(directoryPath, "Missing.slnx");

        try
        {
            var result = await target.GetSolutionHierarchyAsync(solutionPath, TestContext.Current.CancellationToken);

            result.IsSucceeded.Should().BeFalse();
            result.ErrorMessage.Should().Contain(solutionPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_GettingSolutionHierarchy_THEN_ShouldPropagateCancellation()
    {
        var target = new DefaultProjectStructureService();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
            await File.WriteAllTextAsync(solutionPath, CreateSlnxContent(), TestContext.Current.CancellationToken);
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            var action = async () => await target.GetSolutionHierarchyAsync(solutionPath, cancellationSource.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_MissingProjectFile_WHEN_GettingTargetFrameworks_THEN_ShouldReturnFailure()
    {
        var target = new DefaultProjectStructureService();
        var projectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "Missing.csproj");

        var result = target.GetTargetFrameworks(projectPath);

        result.IsSucceeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain(projectPath);
    }

    [Fact]
    public void GIVEN_MalformedProject_WHEN_GettingTargetFrameworks_THEN_ShouldReturnFailure()
    {
        var target = new DefaultProjectStructureService();
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
        var target = new DefaultProjectStructureService();
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
        var target = new DefaultProjectStructureService();
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
        var target = new DefaultProjectStructureService();

        var result = target.GetTargetFrameworks(project);

        result.IsSucceeded.Should().BeTrue();
        result.TargetFrameworks.Should().BeEmpty();
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
