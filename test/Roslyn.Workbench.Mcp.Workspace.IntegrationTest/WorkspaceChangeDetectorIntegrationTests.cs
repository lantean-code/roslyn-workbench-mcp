namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceChangeDetectorIntegrationTests
{
    [Fact]
    public void GIVEN_ProjectWithCustomImportedProps_WHEN_BuildingManifest_THEN_ShouldIncludeEvaluatedImportPath()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var buildDirectoryPath = Path.Combine(directoryPath, "build");
            Directory.CreateDirectory(buildDirectoryPath);

            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            var documentPath = Path.Combine(directoryPath, "Class1.cs");
            var importedPropsPath = Path.Combine(buildDirectoryPath, "Custom.props");

            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="build\Custom.props" />
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(documentPath, """
                namespace Sample;

                public sealed class Class1
                {
                }
                """);
            File.WriteAllText(importedPropsPath, """
                <Project>
                  <PropertyGroup>
                    <LangVersion>preview</LangVersion>
                  </PropertyGroup>
                </Project>
                """);

            using var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var solution = workspace.CurrentSolution
                .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp, filePath: projectPath))
                .AddDocument(DocumentId.CreateNewId(projectId), "Class1.cs", SourceText.From(File.ReadAllText(documentPath)), filePath: documentPath);

            var target = new WorkspaceChangeDetector(new FileSystem(), new WorkspaceProjectInputResolver());

            var manifest = target.BuildManifest(solution, projectPath);

            manifest.Files.Select(static file => file.Path).Should().Contain(importedPropsPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_MalformedProject_WHEN_BuildingManifest_THEN_ShouldRetainEvaluationFailure()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Malformed.csproj");
            File.WriteAllText(projectPath, "<Project><PropertyGroup>");
            using var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "Malformed",
                "Malformed",
                LanguageNames.CSharp,
                filePath: projectPath));
            var target = new WorkspaceChangeDetector(new FileSystem(), new WorkspaceProjectInputResolver());

            var manifest = target.BuildManifest(solution, projectPath);
            var hasChanged = target.HasChanged(manifest, TestContext.Current.CancellationToken);

            manifest.IsComplete.Should().BeFalse();
            manifest.EvaluationFailures.Should().ContainSingle().Which.ProjectPath.Should().Be(projectPath);
            manifest.EvaluationFailures[0].Message.Should().NotBeNullOrWhiteSpace();
            hasChanged.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    private static string CreateDirectoryPath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-manifest-tests", Guid.NewGuid().ToString("n"));
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
