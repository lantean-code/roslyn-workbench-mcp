namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

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

            var fileSystem = new FileSystem();
            var pathComparison = new WorkspacePathComparison();
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(fileSystem, pathComparison);
            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = target.BuildManifest(solution, projectPath, directoryPath);

            manifest.Files.Select(static file => file.Path).Should().Contain(importedPropsPath);
            manifest.PathPolicy.ArtifactRoots.Should().Contain(Path.Combine(directoryPath, ".vs"));
            manifest.PathPolicy.ArtifactRoots.Should().Contain(Path.Combine(directoryPath, "bin"));
            manifest.PathPolicy.ArtifactRoots.Should().Contain(Path.Combine(directoryPath, "obj"));
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_EvaluatedCustomAndCentralArtifactPaths_WHEN_BuildingManifest_THEN_ShouldExcludeOnlyThoseTrees()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectDirectoryPath = Path.Combine(directoryPath, "src", "Sample");
            var conventionalObjDirectoryPath = Path.Combine(projectDirectoryPath, "obj");
            var customIntermediatePath = Path.Combine(directoryPath, "intermediate", "Generated.cs");
            var centralArtifactPath = Path.Combine(directoryPath, "artifacts", "Generated.cs");
            var conventionalNamedSourcePath = Path.Combine(conventionalObjDirectoryPath, "Source.cs");
            Directory.CreateDirectory(projectDirectoryPath);
            Directory.CreateDirectory(conventionalObjDirectoryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(customIntermediatePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(centralArtifactPath)!);

            var buildPropsPath = Path.Combine(directoryPath, "Directory.Build.props");
            var projectPath = Path.Combine(projectDirectoryPath, "Sample.csproj");
            File.WriteAllText(buildPropsPath, """
                <Project>
                  <PropertyGroup>
                    <ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath>
                    <BaseIntermediateOutputPath>$(MSBuildThisFileDirectory)intermediate\</BaseIntermediateOutputPath>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(customIntermediatePath, "class IntermediateGenerated { }");
            File.WriteAllText(centralArtifactPath, "class ArtifactGenerated { }");
            File.WriteAllText(conventionalNamedSourcePath, "class Source { }");

            using var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var solution = workspace.CurrentSolution
                .AddProject(ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    "Sample",
                    "Sample",
                    LanguageNames.CSharp,
                    filePath: projectPath))
                .AddDocument(
                    DocumentId.CreateNewId(projectId),
                    "IntermediateGenerated.cs",
                    SourceText.From("class IntermediateGenerated { }"),
                    filePath: customIntermediatePath)
                .AddDocument(
                    DocumentId.CreateNewId(projectId),
                    "ArtifactGenerated.cs",
                    SourceText.From("class ArtifactGenerated { }"),
                    filePath: centralArtifactPath)
                .AddDocument(
                    DocumentId.CreateNewId(projectId),
                    "Source.cs",
                    SourceText.From("class Source { }"),
                    filePath: conventionalNamedSourcePath);

            var fileSystem = new FileSystem();
            var pathComparison = new WorkspacePathComparison();
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(fileSystem, pathComparison);
            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = target.BuildManifest(solution, projectPath, directoryPath);

            manifest.PathPolicy.ArtifactRoots.Should().Contain(Path.Combine(directoryPath, "artifacts"));
            manifest.PathPolicy.ArtifactRoots.Should().Contain(Path.Combine(directoryPath, "intermediate"));
            manifest.Files.Select(static file => file.Path).Should().Contain(conventionalNamedSourcePath);
            manifest.Files.Select(static file => file.Path).Should().NotContain(customIntermediatePath, centralArtifactPath);
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

            var fileSystem = new FileSystem();
            var pathComparison = new WorkspacePathComparison();
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(fileSystem, pathComparison);
            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = target.BuildManifest(solution, projectPath, directoryPath);
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

    [Fact]
    public void GIVEN_SameLengthAndTimestampEdit_WHEN_CheckingManifest_THEN_ShouldDetectWatcherChange()
    {
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            var documentPath = Path.Combine(directoryPath, "Class1.cs");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(documentPath, "class A { }");
            var originalWriteTime = File.GetLastWriteTimeUtc(documentPath);
            using var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "Sample",
                "Sample",
                LanguageNames.CSharp,
                filePath: projectPath);

            var solution = workspace.CurrentSolution
                .AddProject(projectInfo)
                .AddDocument(DocumentId.CreateNewId(projectId), "Class1.cs", SourceText.From("class A { }"), filePath: documentPath);

            var fileSystem = new FileSystem();
            var pathComparison = new WorkspacePathComparison();
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(fileSystem, pathComparison);
            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = target.BuildManifest(solution, projectPath, directoryPath);

            File.WriteAllText(documentPath, "class B { }");
            File.SetLastWriteTimeUtc(documentPath, originalWriteTime);

            var hasChanged = SpinWait.SpinUntil(
                () => target.HasChanged(manifest, TestContext.Current.CancellationToken),
                TimeSpan.FromSeconds(5));

            hasChanged.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_InputChangesAfterCertificationStarts_WHEN_ManifestIsBuilt_THEN_ShouldReplayBufferedWatcherChange()
    {
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            var documentPath = Path.Combine(directoryPath, "Class1.cs");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(documentPath, "class A { }");
            var originalWriteTime = File.GetLastWriteTimeUtc(documentPath);
            using var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var solution = workspace.CurrentSolution
                .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp, filePath: projectPath))
                .AddDocument(DocumentId.CreateNewId(projectId), "Class1.cs", SourceText.From("class A { }"), filePath: documentPath);

            var fileSystem = new FileSystem();
            var pathComparison = new WorkspacePathComparison();
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(fileSystem, pathComparison);
            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var certification = target.BeginCertification(directoryPath);
            File.WriteAllText(documentPath, "class B { }");
            File.SetLastWriteTimeUtc(documentPath, originalWriteTime);
            using var manifest = target.BuildManifest(solution, projectPath, directoryPath, certification);

            var hasChanged = SpinWait.SpinUntil(
                () => target.HasChanged(manifest, TestContext.Current.CancellationToken),
                TimeSpan.FromSeconds(5));

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
