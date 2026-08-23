using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceChangeDetectorIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_GlobalProperties_WHEN_ResolvingProjectInputs_THEN_ShouldUseThemForImportsAndArtifactRoots()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            var releasePropsPath = Path.Combine(directoryPath, "Release.props");
            var artifactsPath = Path.Combine(directoryPath, "external-artifacts");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="Release.props" Condition="'$(Configuration)' == 'Release'" />
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(releasePropsPath, "<Project />");
            var properties = new WorkspaceMsBuildProperties
            {
                ArtifactsPath = artifactsPath,
                Configuration = "Release",
            };

            var pathComparison = new WorkspacePathComparison();
            var target = new WorkspaceProjectInputResolver(pathComparison);

            var result = target.Resolve(projectPath, properties);

            result.IsSucceeded.Should().BeTrue();
            result.ImportedPaths.Should().Contain(releasePropsPath);
            result.ArtifactRoots.Should().Contain(artifactsPath);
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ExternalWorkspaceItemGlobs_WHEN_ResolvingProjectInputs_THEN_ShouldRetainEvaluatedMembershipRules()
    {
        MsBuildTestRegistration.EnsureRegistered();
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-external-glob-tests");
        var workspaceRoot = Path.Combine(directory.DirectoryPath, "workspace");
        var externalRoot = Path.Combine(directory.DirectoryPath, "external");
        var excludedRoot = Path.Combine(externalRoot, "excluded");
        var removedRoot = Path.Combine(externalRoot, "removed");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(excludedRoot);
        Directory.CreateDirectory(removedRoot);

        var projectPath = Path.Combine(workspaceRoot, "Sample.csproj");
        var includedSourcePath = Path.Combine(externalRoot, "Included.cs");
        var excludedSourcePath = Path.Combine(excludedRoot, "Excluded.cs");
        var removedSourcePath = Path.Combine(removedRoot, "Removed.cs");
        var additionalPath = Path.Combine(externalRoot, "Additional.txt");
        var editorConfigPath = Path.Combine(externalRoot, "Settings.globalconfig");
        var externalInclude = GetProjectRelativePath(workspaceRoot, externalRoot);
        File.WriteAllText(projectPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="{{externalInclude}}/**/*.cs" Exclude="{{externalInclude}}/excluded/**/*.cs" />
                <Compile Remove="{{externalInclude}}/removed/**/*.cs" />
                <AdditionalFiles Include="{{externalInclude}}/**/*.txt" />
                <EditorConfigFiles Include="{{externalInclude}}/**/*.globalconfig" />
              </ItemGroup>
            </Project>
            """);

        var pathComparison = new WorkspacePathComparison();
        var target = new WorkspaceProjectInputResolver(pathComparison);

        var result = target.Resolve(projectPath);

        result.IsSucceeded.Should().BeTrue();
        result.ItemGlobs.SelectMany(static glob => glob.SearchRoots).Should().Contain(externalRoot);
        result.ItemGlobs.Should().Contain(glob => glob.Matches(includedSourcePath));
        result.ItemGlobs.Should().Contain(glob => glob.Matches(additionalPath));
        result.ItemGlobs.Should().Contain(glob => glob.Matches(editorConfigPath));
        result.ItemGlobs.Should().NotContain(glob => glob.Matches(excludedSourcePath));
        result.ItemGlobs.Should().NotContain(glob => glob.Matches(removedSourcePath));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_OpenProjectWithExternalCompileGlob_WHEN_MatchingFileIsCreated_THEN_ShouldBecomeStaleAndReloadTheDocument()
    {
        MsBuildTestRegistration.EnsureRegistered();
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-external-glob-tests");
        var workspaceRoot = Path.Combine(directory.DirectoryPath, "workspace");
        var externalRoot = Path.Combine(directory.DirectoryPath, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);

        var projectPath = Path.Combine(workspaceRoot, "Sample.csproj");
        var localSourcePath = Path.Combine(workspaceRoot, "Sample.cs");
        var existingExternalPath = Path.Combine(externalRoot, "Existing.cs");
        var createdExternalPath = Path.Combine(externalRoot, "Created.cs");
        var externalInclude = GetProjectRelativePath(workspaceRoot, externalRoot);
        var cancellationToken = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(projectPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup Condition="'$(DesignTimeBuild)' == 'true'">
                <Compile Include="{{externalInclude}}/**/*.cs" />
              </ItemGroup>
            </Project>
            """, cancellationToken);

        await File.WriteAllTextAsync(localSourcePath, "internal sealed class Sample { }", cancellationToken);
        await File.WriteAllTextAsync(existingExternalPath, "internal sealed class Existing { }", cancellationToken);

        using var initialWorkspace = MSBuildWorkspace.Create();
        var initialProject = await initialWorkspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        initialProject.Documents.Should().Contain(document => PathEquals(document.FilePath, existingExternalPath));
        initialProject.Documents.Should().NotContain(document => PathEquals(document.FilePath, createdExternalPath));

        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
        var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
            fileSystem,
            pathComparison,
            externalMonitorFactory);
        var projectInputResolver = new WorkspaceProjectInputResolver(pathComparison);

        var target = new WorkspaceChangeDetector(
            fileSystem,
            projectInputResolver,
            changeMonitorFactory,
            pathComparison);

        using var manifest = BuildCertifiedManifest(
            target,
            initialProject.Solution,
            projectPath,
            workspaceRoot,
            cancellationToken);

        target.HasChanged(manifest, cancellationToken).Should().BeFalse();
        await File.WriteAllTextAsync(createdExternalPath, "internal sealed class Created { }", cancellationToken);

        var detectedChange = SpinWait.SpinUntil(
            () => target.HasChanged(manifest, cancellationToken),
            TimeSpan.FromSeconds(5));

        detectedChange.Should().BeTrue();
        var expectedChange = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Created,
            Path = createdExternalPath,
        };

        manifest.Change.Should().BeEquivalentTo(expectedChange);

        using var reloadedWorkspace = MSBuildWorkspace.Create();
        var reloadedProject = await reloadedWorkspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        reloadedProject.Documents.Should().Contain(document => PathEquals(document.FilePath, createdExternalPath));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_OpenProjectWithExternalCompileGlob_WHEN_PopulatedDirectoryIsMovedIntoRoot_THEN_ShouldBecomeStale()
    {
        MsBuildTestRegistration.EnsureRegistered();
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-external-glob-tests");
        var workspaceRoot = Path.Combine(directory.DirectoryPath, "workspace");
        var externalRoot = Path.Combine(directory.DirectoryPath, "external");
        var sourceDirectory = Path.Combine(directory.DirectoryPath, "source");
        var insertedDirectory = Path.Combine(externalRoot, "inserted");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);
        Directory.CreateDirectory(sourceDirectory);

        var projectPath = Path.Combine(workspaceRoot, "Sample.csproj");
        var localSourcePath = Path.Combine(workspaceRoot, "Sample.cs");
        var insertedSourcePath = Path.Combine(insertedDirectory, "Inserted.cs");
        var sourcePath = Path.Combine(sourceDirectory, "Inserted.cs");
        var externalInclude = GetProjectRelativePath(workspaceRoot, externalRoot);
        var cancellationToken = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(projectPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="{{externalInclude}}/**/*.cs" />
              </ItemGroup>
            </Project>
            """, cancellationToken);

        await File.WriteAllTextAsync(localSourcePath, "internal sealed class Sample { }", cancellationToken);
        await File.WriteAllTextAsync(sourcePath, "internal sealed class Inserted { }", cancellationToken);

        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        project.Documents.Should().NotContain(document => PathEquals(document.FilePath, insertedSourcePath));

        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
        var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
            fileSystem,
            pathComparison,
            externalMonitorFactory);

        var projectInputResolver = new WorkspaceProjectInputResolver(pathComparison);
        var target = new WorkspaceChangeDetector(
            fileSystem,
            projectInputResolver,
            changeMonitorFactory,
            pathComparison);

        using var manifest = BuildCertifiedManifest(
            target,
            project.Solution,
            projectPath,
            workspaceRoot,
            cancellationToken);

        target.HasChanged(manifest, cancellationToken).Should().BeFalse();
        Directory.Move(sourceDirectory, insertedDirectory);

        var detectedChange = SpinWait.SpinUntil(
            () => target.HasChanged(manifest, cancellationToken),
            TimeSpan.FromSeconds(5));

        detectedChange.Should().BeTrue();
        var expectedChange = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = WorkspaceInputChangeKind.Created,
            Path = insertedSourcePath,
        };

        manifest.Change.Should().BeEquivalentTo(expectedChange);
    }

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
            var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
                fileSystem,
                pathComparison,
                externalMonitorFactory);

            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = BuildCertifiedManifest(
                target,
                solution,
                projectPath,
                directoryPath,
                TestContext.Current.CancellationToken);

            manifest.Files.Select(static file => file.Path).Should().Contain(importedPropsPath);
            manifest.PathPolicy.ExcludedDirectoryRoots.Should().Contain(Path.Combine(directoryPath, ".vs"));
            manifest.PathPolicy.ExcludedDirectoryRoots.Should().Contain(Path.Combine(directoryPath, "bin"));
            manifest.PathPolicy.ExcludedDirectoryRoots.Should().Contain(Path.Combine(directoryPath, "obj"));
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_EvaluatedArtifactPaths_WHEN_BuildingManifest_THEN_ShouldExcludeTreesAndPollLoadedDocuments()
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
            var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
                fileSystem,
                pathComparison,
                externalMonitorFactory);

            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = BuildCertifiedManifest(
                target,
                solution,
                projectPath,
                directoryPath,
                TestContext.Current.CancellationToken);

            manifest.PathPolicy.ExcludedDirectoryRoots.Should().Contain(Path.Combine(directoryPath, "artifacts"));
            manifest.PathPolicy.ExcludedDirectoryRoots.Should().Contain(Path.Combine(directoryPath, "intermediate"));
            var manifestDirectoryPaths = manifest.Directories.Select(static directory => directory.Path);
            manifestDirectoryPaths.Should().Contain(projectDirectoryPath);
            manifestDirectoryPaths.Should().NotContain(
                Path.Combine(directoryPath, "artifacts"),
                Path.Combine(directoryPath, "intermediate"));

            manifest.Files.Select(static file => file.Path).Should().Contain(
                conventionalNamedSourcePath,
                customIntermediatePath,
                centralArtifactPath);

            File.WriteAllText(customIntermediatePath, "class ChangedIntermediateGenerated { }");

            target.HasChanged(manifest, TestContext.Current.CancellationToken).Should().BeTrue();
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
            var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
                fileSystem,
                pathComparison,
                externalMonitorFactory);

            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = BuildCertifiedManifest(
                target,
                solution,
                projectPath,
                directoryPath,
                TestContext.Current.CancellationToken);

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
            var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
                fileSystem,
                pathComparison,
                externalMonitorFactory);

            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var manifest = BuildCertifiedManifest(target, solution, projectPath, directoryPath, TestContext.Current.CancellationToken);

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
            var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
                fileSystem,
                pathComparison,
                externalMonitorFactory);

            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            using var certification = target.BeginCertification(directoryPath);
            File.WriteAllText(documentPath, "class B { }");
            File.SetLastWriteTimeUtc(documentPath, originalWriteTime);
            using var manifest = target.BuildManifest(
                solution,
                projectPath,
                directoryPath,
                certification,
                null,
                TestContext.Current.CancellationToken);

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
    public async Task GIVEN_WindowsAtomicReplacements_WHEN_CommitInputsAreCertified_THEN_ShouldIgnoreNativeTransientPaths()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectDirectoryPath = Path.Combine(directoryPath, "Project");
            Directory.CreateDirectory(projectDirectoryPath);
            var destinationPaths = Enumerable.Range(0, 32)
                .Select(index => Path.Combine(projectDirectoryPath, $"Document{index}.cs"))
                .ToArray();

            foreach (var destinationPath in destinationPaths)
            {
                await File.WriteAllTextAsync(
                    destinationPath,
                    "class Original { }",
                    TestContext.Current.CancellationToken);
            }

            var fileSystem = new FileSystem();
            var pathComparison = new WorkspacePathComparison();
            var externalMonitorFactory = new WorkspaceExternalInputChangeMonitorFactory(fileSystem, pathComparison);
            var changeMonitorFactory = new WorkspaceInputChangeMonitorFactory(
                fileSystem,
                pathComparison,
                externalMonitorFactory);

            var target = new WorkspaceChangeDetector(
                fileSystem,
                new WorkspaceProjectInputResolver(pathComparison),
                changeMonitorFactory,
                pathComparison);

            var atomicFileCommitter = new NativeAtomicFileCommitter();
            var atomicFileWriter = new AtomicFileWriter(fileSystem, atomicFileCommitter);
            using var certification = target.BeginCertification(directoryPath);

            foreach (var destinationPath in destinationPaths)
            {
                await atomicFileWriter.WriteAllBytesAsync(
                    destinationPath,
                    "class Replacement { }"u8.ToArray(),
                    AtomicFileAccess.Default,
                    TestContext.Current.CancellationToken);
            }

            using var originalManifest = new WorkspaceInputManifest
            {
                Directories =
                [
                    new WorkspaceInputDirectoryFingerprint
                    {
                        Path = projectDirectoryPath,
                    },
                ],
                Files = destinationPaths
                    .Select(static path => new WorkspaceInputFileFingerprint
                    {
                        Path = path,
                    })
                    .ToArray(),
            };

            using var completedManifest = certification.Complete(originalManifest, destinationPaths);
            var detectedChange = SpinWait.SpinUntil(
                () => target.HasChanged(completedManifest, TestContext.Current.CancellationToken),
                TimeSpan.FromSeconds(1));

            detectedChange.Should().BeFalse();
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

    private static WorkspaceInputManifest BuildCertifiedManifest(
        WorkspaceChangeDetector target,
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        using var certification = target.BeginCertification(workspaceRoot);
        return target.BuildManifest(
            solution,
            loadedPath,
            workspaceRoot,
            certification,
            null,
            cancellationToken);
    }

    private static void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static string GetProjectRelativePath(string projectDirectory, string path)
    {
        return Path.GetRelativePath(projectDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool PathEquals(string? left, string right)
    {
        return left is not null
            && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.Ordinal);
    }
}
