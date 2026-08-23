using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceChangeDetectorTests : IDisposable
{
    private static readonly DateTime LastWriteTimeUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFileInfoFactory> _fileInfoFactory;
    private readonly Mock<IDirectoryInfoFactory> _directoryInfoFactory;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly Mock<IWorkspaceProjectInputResolver> _projectInputResolver;
    private readonly Mock<IWorkspaceInputChangeMonitorFactory> _changeMonitorFactory;
    private readonly Mock<IWorkspaceInputChangeMonitor> _changeMonitor;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly WorkspaceChangeDetector _target;

    public WorkspaceChangeDetectorTests()
    {
        _workspace = new AdhocWorkspace();
        _fileSystem = new Mock<IFileSystem>();
        _fileInfoFactory = new Mock<IFileInfoFactory>();
        _directoryInfoFactory = new Mock<IDirectoryInfoFactory>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _projectInputResolver = new Mock<IWorkspaceProjectInputResolver>();
        _changeMonitorFactory = new Mock<IWorkspaceInputChangeMonitorFactory>();
        _changeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _fileSystem.SetupGet(item => item.FileInfo).Returns(_fileInfoFactory.Object);
        _fileSystem.SetupGet(item => item.DirectoryInfo).Returns(_directoryInfoFactory.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _directory
            .Setup(item => item.EnumerateDirectories(It.IsAny<string>(), "*", SearchOption.TopDirectoryOnly))
            .Returns([]);

        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns((string path) => Path.GetDirectoryName(path));
        _path.Setup(item => item.GetFileName(It.IsAny<string>())).Returns((string path) => Path.GetFileName(path));
        _path.Setup(item => item.GetFullPath(It.IsAny<string>())).Returns((string path) => Path.GetFullPath(path));
        _path.Setup(item => item.TrimEndingDirectorySeparator(It.IsAny<string>())).Returns((string path) => Path.TrimEndingDirectorySeparator(path));
        _path.SetupGet(item => item.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        _path.Setup(item => item.EndsInDirectorySeparator(It.IsAny<string>())).Returns((string path) => Path.EndsInDirectorySeparator(path));
        _path.Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string left, string right) => Path.Combine(left, right));
        _changeMonitorFactory
            .Setup(item => item.Create(It.IsAny<string>()))
            .Returns(_changeMonitor.Object);
        _pathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));

        _target = new WorkspaceChangeDetector(
            _fileSystem.Object,
            _projectInputResolver.Object,
            _changeMonitorFactory.Object,
            _pathComparison.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_InvalidLoadedPath_WHEN_BuildingManifest_THEN_ShouldThrowArgumentException(string loadedPath)
    {
        var action = () => BuildCertifiedManifest(_workspace.CurrentSolution, loadedPath, "/Workspace");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_InMemorySolutionWithWorkspaceInputs_WHEN_BuildingManifest_THEN_ShouldCaptureDistinctTrackedInputs()
    {
        var root = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(root, "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var sourcePath = Path.Combine(projectDirectory, "Document.cs");
        var additionalPath = Path.Combine(projectDirectory, "Additional.txt");
        var editorConfigPath = Path.Combine(projectDirectory, ".editorconfig");
        var generatedPath = Path.Combine(projectDirectory, "obj", "Debug", "Generated.cs");
        var analyzerPath = Path.Combine(root, "Analyzers", "Analyzer.dll");
        var referencePath = typeof(object).Assembly.Location;
        var importPath = Path.Combine(root, "Build", "Imported.props");
        var solutionPath = Path.Combine(root, "Workspace.sln");
        var properties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = Path.Combine(root, "Artifacts"),
            Configuration = "Release",
        };

        var projectId = ProjectId.CreateNewId();
        var analyzerReference = new Mock<AnalyzerReference>();
        analyzerReference.SetupGet(item => item.Display).Returns(analyzerPath);
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath))
            .AddDocument(DocumentId.CreateNewId(projectId), "Document.cs", SourceText.From("class Document { }"), filePath: sourcePath)
            .AddDocument(DocumentId.CreateNewId(projectId), "Generated.cs", SourceText.From("class Generated { }"), filePath: generatedPath)
            .AddAdditionalDocument(DocumentId.CreateNewId(projectId), "Additional.txt", SourceText.From("Additional"), filePath: additionalPath)
            .AddAnalyzerConfigDocument(DocumentId.CreateNewId(projectId), ".editorconfig", SourceText.From("root = true"), filePath: editorConfigPath)
            .AddAnalyzerReference(projectId, analyzerReference.Object)
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(referencePath));

        foreach (var filePath in new[] { solutionPath, projectPath, sourcePath, additionalPath, editorConfigPath, generatedPath, analyzerPath, referencePath, importPath })
        {
            SetupFile(filePath);
            SetupDirectory(Path.GetDirectoryName(filePath)!);
        }

        var sourceDirectory = Path.Combine(projectDirectory, "Source");
        var binDirectory = Path.Combine(projectDirectory, "bin");
        var objDirectory = Path.Combine(projectDirectory, "obj");
        SetupDirectory(sourceDirectory);
        _projectInputResolver.Setup(item => item.Resolve(projectPath, properties))
            .Returns(WorkspaceProjectInputResolution.Succeeded([importPath]));

        _directory.Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly)).Returns(
            [sourceDirectory, binDirectory, objDirectory]);

        using var certification = _target.BeginCertification(root);
        var result = _target.BuildManifest(
            solution,
            solutionPath,
            root,
            certification,
            properties,
            TestContext.Current.CancellationToken);

        result.Files.Select(item => item.Path).Should().BeEquivalentTo(
            solutionPath,
            projectPath,
            sourcePath,
            additionalPath,
            editorConfigPath,
            generatedPath,
            analyzerPath,
            referencePath,
            importPath);

        result.Directories.Select(item => item.Path).Should().Contain(sourceDirectory);
        result.Directories.Select(item => item.Path).Should().NotContain(binDirectory, objDirectory);
        _changeMonitorFactory.Verify(item => item.Create(root), Times.Once);
        _changeMonitor.Verify(item => item.Start(), Times.Once);
        _changeMonitor.Verify(item => item.Track(result), Times.Once);
        _projectInputResolver.Verify(item => item.Resolve(projectPath, properties), Times.Once);
    }

    [Fact]
    public void GIVEN_ExcludedDirectoryTree_WHEN_BuildingManifest_THEN_ShouldNotEnterExcludedRoot()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(workspaceRoot, "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var sourceDirectory = Path.Combine(projectDirectory, "Source");
        var artifactDirectory = Path.Combine(projectDirectory, "obj");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath);

        var solution = _workspace.CurrentSolution.AddProject(projectInfo);

        SetupFile(solutionPath);
        SetupFile(projectPath);
        SetupDirectory(workspaceRoot);
        SetupDirectory(projectDirectory);
        SetupDirectory(sourceDirectory);
        SetupDirectory(artifactDirectory);
        _directory
            .Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([sourceDirectory, artifactDirectory]);

        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded(artifactRoots: [artifactDirectory]));

        using var manifest = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);

        var manifestDirectoryPaths = manifest.Directories.Select(static directory => directory.Path);
        manifestDirectoryPaths.Should().Contain(sourceDirectory);
        manifestDirectoryPaths.Should().NotContain(artifactDirectory);
        _directory.Verify(
            item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly),
            Times.Once);

        _directory.Verify(
            item => item.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly),
            Times.Once);

        _directory.Verify(
            item => item.EnumerateDirectories(artifactDirectory, It.IsAny<string>(), It.IsAny<SearchOption>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_NestedProjectRoots_WHEN_BuildingManifest_THEN_ShouldTraverseEachDirectoryOnce()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var parentDirectory = Path.Combine(workspaceRoot, "Project");
        var nestedDirectory = Path.Combine(parentDirectory, "Nested");
        var sourceDirectory = Path.Combine(nestedDirectory, "Source");
        var parentProjectPath = Path.Combine(parentDirectory, "Parent.csproj");
        var nestedProjectPath = Path.Combine(nestedDirectory, "Nested.csproj");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var parentProject = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Parent",
            "Parent",
            LanguageNames.CSharp,
            filePath: parentProjectPath);

        var nestedProject = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Nested",
            "Nested",
            LanguageNames.CSharp,
            filePath: nestedProjectPath);

        var solution = _workspace.CurrentSolution
            .AddProject(parentProject)
            .AddProject(nestedProject);

        SetupFile(solutionPath);
        SetupFile(parentProjectPath);
        SetupFile(nestedProjectPath);
        SetupDirectory(workspaceRoot);
        SetupDirectory(parentDirectory);
        SetupDirectory(nestedDirectory);
        SetupDirectory(sourceDirectory);
        _directory
            .Setup(item => item.EnumerateDirectories(parentDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([nestedDirectory, nestedDirectory]);

        _directory
            .Setup(item => item.EnumerateDirectories(nestedDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([sourceDirectory]);

        _projectInputResolver
            .Setup(item => item.Resolve(parentProjectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        _projectInputResolver
            .Setup(item => item.Resolve(nestedProjectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        using var manifest = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);

        var manifestDirectoryPaths = manifest.Directories.Select(static directory => directory.Path);
        manifestDirectoryPaths.Should().Contain(parentDirectory, nestedDirectory, sourceDirectory);
        _directory.Verify(
            item => item.EnumerateDirectories(parentDirectory, "*", SearchOption.TopDirectoryOnly),
            Times.Once);

        _directory.Verify(
            item => item.EnumerateDirectories(nestedDirectory, "*", SearchOption.TopDirectoryOnly),
            Times.Once);

        _directory.Verify(
            item => item.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly),
            Times.Once);
    }

    [Fact]
    public void GIVEN_CancellationDuringDirectoryTraversal_WHEN_BuildingManifest_THEN_ShouldPropagateCancellation()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(workspaceRoot, "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var childDirectory = Path.Combine(projectDirectory, "Child");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath);

        var solution = _workspace.CurrentSolution.AddProject(projectInfo);
        using var cancellationSource = new CancellationTokenSource();

        SetupFile(solutionPath);
        SetupFile(projectPath);
        SetupDirectory(workspaceRoot);
        SetupDirectory(projectDirectory);
        SetupDirectory(childDirectory);
        _directory
            .Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns(() =>
            {
                cancellationSource.Cancel();
                return [childDirectory];
            });

        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        using var certification = _target.BeginCertification(workspaceRoot);
        var action = () => _target.BuildManifest(
            solution,
            solutionPath,
            workspaceRoot,
            certification,
            null,
            cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
        _directory.Verify(
            item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly),
            Times.Once);

        _directory.Verify(
            item => item.EnumerateDirectories(childDirectory, It.IsAny<string>(), It.IsAny<SearchOption>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_CaseSensitiveExternalPathsAndCaseInsensitiveWorkspace_WHEN_BuildingManifest_THEN_ShouldRetainBothFiles()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(workspaceRoot, "Project");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var upperCaseExternalPath = Path.GetFullPath("/External/Document.cs");
        var lowerCaseExternalPath = Path.GetFullPath("/External/document.cs");
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath))
            .AddDocument(DocumentId.CreateNewId(projectId), "Upper.cs", SourceText.From("class Upper { }"), filePath: upperCaseExternalPath)
            .AddDocument(DocumentId.CreateNewId(projectId), "Lower.cs", SourceText.From("class Lower { }"), filePath: lowerCaseExternalPath);

        _pathComparison
            .Setup(item => item.GetComparison(workspaceRoot))
            .Returns(StringComparison.OrdinalIgnoreCase);
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(
                path,
                isCaseSensitive: !path.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase)));
        foreach (var filePath in new[] { solutionPath, projectPath, upperCaseExternalPath, lowerCaseExternalPath })
        {
            SetupFile(filePath);
            SetupDirectory(Path.GetDirectoryName(filePath)!);
        }

        _directory
            .Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([]);
        _projectInputResolver.Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        using var manifest = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);

        manifest.Files.Select(static file => file.Path).Should().Contain(
            upperCaseExternalPath,
            lowerCaseExternalPath);
    }

    [Fact]
    public void GIVEN_CaseDistinctProjectsOnNestedCaseSensitiveFileSystem_WHEN_BuildingManifest_THEN_ShouldResolveBothProjects()
    {
        using var workspace = new AdhocWorkspace();
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var nestedRoot = Path.Combine(workspaceRoot, "Native");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var upperCaseProjectPath = Path.Combine(nestedRoot, "Project.csproj");
        var lowerCaseProjectPath = Path.Combine(nestedRoot, "project.csproj");
        var upperCaseProject = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Upper", "Upper", LanguageNames.CSharp, filePath: upperCaseProjectPath);
        var lowerCaseProject = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lower", "Lower", LanguageNames.CSharp, filePath: lowerCaseProjectPath);
        var solution = workspace.CurrentSolution
            .AddProject(upperCaseProject)
            .AddProject(lowerCaseProject);

        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(
                path,
                isCaseSensitive: path.StartsWith(nestedRoot, StringComparison.Ordinal)));

        SetupFile(solutionPath);
        SetupFile(upperCaseProjectPath);
        SetupFile(lowerCaseProjectPath);
        SetupDirectory(workspaceRoot);
        SetupDirectory(nestedRoot);
        _directory
            .Setup(item => item.EnumerateDirectories(nestedRoot, "*", SearchOption.TopDirectoryOnly))
            .Returns([]);

        _projectInputResolver
            .Setup(item => item.Resolve(upperCaseProjectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        _projectInputResolver
            .Setup(item => item.Resolve(lowerCaseProjectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        using var manifest = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);

        _projectInputResolver.Verify(item => item.Resolve(upperCaseProjectPath), Times.Once);
        _projectInputResolver.Verify(item => item.Resolve(lowerCaseProjectPath), Times.Once);
    }

    [Fact]
    public void GIVEN_ManifestConstructionFailure_WHEN_BuildingManifest_THEN_ShouldDisposeChangeMonitor()
    {
        _fileInfoFactory
            .Setup(item => item.New("/Workspace/Workspace.sln"))
            .Throws(new IOException());

        var action = () => BuildCertifiedManifest(
            _workspace.CurrentSolution,
            "/Workspace/Workspace.sln",
            "/Workspace");

        action.Should().Throw<IOException>();
        _changeMonitor.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_MonitorCannotStart_WHEN_BeginningCertification_THEN_ShouldDisposeMonitorAndPropagateFailure()
    {
        _changeMonitor.Setup(item => item.Start()).Throws(new IOException("Watcher failed."));

        var action = () => _target.BeginCertification("/Workspace");

        action.Should().Throw<IOException>().WithMessage("Watcher failed.");
        _changeMonitor.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_IgnoredInputsAreUnavailable_WHEN_CheckingForChanges_THEN_ShouldSkipThoseInputs()
    {
        const string directoryPath = "/Workspace/Project";
        const string filePath = "/Workspace/Project/Document.cs";
        var directory = new WorkspaceInputDirectoryFingerprint { Path = directoryPath };
        var file = new WorkspaceInputFileFingerprint { Path = filePath };
        var ignoredPaths = new HashSet<FileSystemPathKey>
        {
            new FileSystemPathKey(directoryPath, isCaseSensitive: true),
            new FileSystemPathKey(filePath, isCaseSensitive: true),
        };

        using var manifest = new WorkspaceInputManifest
        {
            Directories = [directory],
            Files = [file],
            IgnoredPaths = ignoredPaths,
        };

        var result = _target.HasChanged(manifest, CancellationToken.None);

        result.Should().BeFalse();
        _directoryInfoFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
        _fileInfoFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_CustomArtifactRoots_WHEN_BuildingManifest_THEN_ShouldUseEvaluatedRootsRatherThanDirectoryNames()
    {
        var root = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(root, "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var conventionalObjDirectory = Path.Combine(projectDirectory, "obj");
        var customObjDirectory = Path.Combine(projectDirectory, "custom-obj");
        var conventionalNamedSourcePath = Path.Combine(conventionalObjDirectory, "Source.cs");
        var generatedPath = Path.Combine(customObjDirectory, "Generated.cs");
        var solutionPath = Path.Combine(root, "Workspace.sln");
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "Project",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath))
            .AddDocument(
                DocumentId.CreateNewId(projectId),
                "Source.cs",
                SourceText.From("class Source { }"),
                filePath: conventionalNamedSourcePath)
            .AddDocument(
                DocumentId.CreateNewId(projectId),
                "Generated.cs",
                SourceText.From("class Generated { }"),
                filePath: generatedPath);

        foreach (var filePath in new[] { solutionPath, projectPath, conventionalNamedSourcePath, generatedPath })
        {
            SetupFile(filePath);
            SetupDirectory(Path.GetDirectoryName(filePath)!);
        }

        _directory
            .Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([conventionalObjDirectory, customObjDirectory]);
        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded(
                artifactRoots: [Path.Combine(projectDirectory, "custom-bin"), customObjDirectory]));

        var result = BuildCertifiedManifest(solution, solutionPath, root);

        result.Files.Select(static file => file.Path).Should().Contain(conventionalNamedSourcePath, generatedPath);
        result.Directories.Select(static directory => directory.Path).Should().Contain(conventionalObjDirectory);
        result.Directories.Select(static directory => directory.Path).Should().NotContain(customObjDirectory);
    }

    [Fact]
    public void GIVEN_MultipleTargetFrameworkProjects_WHEN_BuildingManifest_THEN_ShouldEvaluatePhysicalProjectOnce()
    {
        var root = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(root, "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var solutionPath = Path.Combine(root, "Workspace.sln");
        var firstProjectId = ProjectId.CreateNewId();
        var secondProjectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                firstProjectId,
                VersionStamp.Create(),
                "Project (net9.0)",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath))
            .AddProject(ProjectInfo.Create(
                secondProjectId,
                VersionStamp.Create(),
                "Project (net10.0)",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath));

        SetupFile(solutionPath);
        SetupFile(projectPath);
        SetupDirectory(root);
        SetupDirectory(projectDirectory);
        _directory
            .Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([]);
        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded(
                artifactRoots: [Path.Combine(projectDirectory, "bin"), Path.Combine(projectDirectory, "obj")]));

        using var result = BuildCertifiedManifest(solution, solutionPath, root);

        result.IsComplete.Should().BeTrue();
        _projectInputResolver.Verify(item => item.Resolve(projectPath), Times.Once);
    }

    [Fact]
    public void GIVEN_NestedExternalGlobRoots_WHEN_BuildingManifest_THEN_ShouldCreateOneMinimalMembership()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(workspaceRoot, "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var externalRoot = Path.GetFullPath("/External");
        var nestedExternalRoot = Path.Combine(externalRoot, "Nested");
        var externalPath = Path.Combine(externalRoot, "External.cs");
        var nestedExternalPath = Path.Combine(nestedExternalRoot, "Nested.cs");
        var nonMatchingExternalPath = Path.Combine(externalRoot, "Notes.txt");
        var localPath = Path.Combine(projectDirectory, "Local.cs");
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "Project",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath))
            .AddDocument(DocumentId.CreateNewId(projectId), "Local.cs", SourceText.From("class Local { }"), filePath: localPath)
            .AddDocument(DocumentId.CreateNewId(projectId), "External.cs", SourceText.From("class External { }"), filePath: externalPath)
            .AddDocument(DocumentId.CreateNewId(projectId), "Nested.cs", SourceText.From("class Nested { }"), filePath: nestedExternalPath)
            .AddAdditionalDocument(DocumentId.CreateNewId(projectId), "Notes.txt", SourceText.From("Notes"), filePath: nonMatchingExternalPath);

        var externalMatcher = new Mock<IWorkspaceItemGlobMatcher>();
        externalMatcher
            .Setup(item => item.Matches(It.IsAny<string>()))
            .Returns((string path) => string.Equals(Path.GetExtension(path), ".cs", StringComparison.Ordinal));

        var nestedMatcher = new Mock<IWorkspaceItemGlobMatcher>();
        nestedMatcher
            .Setup(item => item.Matches(It.IsAny<string>()))
            .Returns((string path) => path.StartsWith(nestedExternalRoot, StringComparison.Ordinal));

        var externalGlob = new WorkspaceEvaluatedItemGlob(
            externalMatcher.Object,
            [projectDirectory, externalRoot, nestedExternalRoot]);
        var nestedGlob = new WorkspaceEvaluatedItemGlob(nestedMatcher.Object, [nestedExternalRoot]);
        SetupFile(solutionPath);
        SetupFile(projectPath);
        SetupFile(localPath);
        SetupFile(externalPath);
        SetupFile(nestedExternalPath);
        SetupFile(nonMatchingExternalPath);
        SetupDirectory(workspaceRoot);
        SetupDirectory(projectDirectory);
        SetupDirectory(externalRoot);
        SetupDirectory(nestedExternalRoot);
        _directory
            .Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([]);

        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded(itemGlobs: [externalGlob, nestedGlob]));

        using var result = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);

        var membership = result.ExternalInputMemberships.Should().ContainSingle().Which;
        membership.SearchRoot.Should().Be(externalRoot);
        membership.Globs.Should().Contain(externalGlob);
        membership.Globs.Should().Contain(nestedGlob);
        membership.LoadedPaths.Should().Contain(_pathComparison.Object.CreateKey(externalPath));
        membership.LoadedPaths.Should().Contain(_pathComparison.Object.CreateKey(nestedExternalPath));
        membership.LoadedPaths.Should().NotContain(_pathComparison.Object.CreateKey(nonMatchingExternalPath));
    }

    [Fact]
    public void GIVEN_OneGlobWithDisjointExternalRoots_WHEN_BuildingManifest_THEN_ShouldScopeLoadedPathsToEachRoot()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectDirectory = Path.Combine(workspaceRoot, "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var firstExternalRoot = Path.GetFullPath("/FirstExternal");
        var secondExternalRoot = Path.GetFullPath("/SecondExternal");
        var firstExternalPath = Path.Combine(firstExternalRoot, "First.cs");
        var secondExternalPath = Path.Combine(secondExternalRoot, "Second.cs");
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "Project",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath))
            .AddDocument(DocumentId.CreateNewId(projectId), "First.cs", SourceText.From("class First { }"), filePath: firstExternalPath)
            .AddDocument(DocumentId.CreateNewId(projectId), "Second.cs", SourceText.From("class Second { }"), filePath: secondExternalPath);

        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        matcher.Setup(item => item.Matches(It.IsAny<string>())).Returns(true);
        var glob = new WorkspaceEvaluatedItemGlob(
            matcher.Object,
            [firstExternalRoot, secondExternalRoot]);

        SetupFile(solutionPath);
        SetupFile(projectPath);
        SetupFile(firstExternalPath);
        SetupFile(secondExternalPath);
        SetupDirectory(workspaceRoot);
        SetupDirectory(projectDirectory);
        SetupDirectory(firstExternalRoot);
        SetupDirectory(secondExternalRoot);
        _directory
            .Setup(item => item.EnumerateDirectories(projectDirectory, "*", SearchOption.TopDirectoryOnly))
            .Returns([]);
        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded(itemGlobs: [glob]));

        using var result = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);

        result.ExternalInputMemberships.Should().HaveCount(2);
        var firstMembership = result.ExternalInputMemberships.Single(item => item.SearchRoot == firstExternalRoot);
        var secondMembership = result.ExternalInputMemberships.Single(item => item.SearchRoot == secondExternalRoot);
        firstMembership.LoadedPaths.Should().BeEquivalentTo([_pathComparison.Object.CreateKey(firstExternalPath)]);
        secondMembership.LoadedPaths.Should().BeEquivalentTo([_pathComparison.Object.CreateKey(secondExternalPath)]);
    }

    [Fact]
    public void GIVEN_MissingInputs_WHEN_BuildingManifest_THEN_ShouldIgnoreThem()
    {
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: "/Missing/Project.csproj"));

        SetupMissingFile("/Missing/Workspace.sln");
        SetupMissingFile("/Missing/Project.csproj");
        _projectInputResolver.Setup(item => item.Resolve("/Missing/Project.csproj"))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        var result = BuildCertifiedManifest(solution, "/Missing/Workspace.sln", "/Missing");

        result.Files.Should().BeEmpty();
        result.Directories.Should().BeEmpty();
        _directory.Verify(item => item.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ProjectWithoutExistingDirectory_WHEN_BuildingManifest_THEN_ShouldNotEnumerateDirectories()
    {
        var projectDirectory = Path.GetFullPath("/Workspace/Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var missingRoot = Path.GetFullPath("/Missing");
        var missingSolutionPath = Path.Combine(missingRoot, "Workspace.sln");
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath));

        SetupMissingFile(missingSolutionPath);
        SetupFile(projectPath);
        SetupMissingDirectory(projectDirectory);
        _projectInputResolver.Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        var result = BuildCertifiedManifest(solution, missingSolutionPath, missingRoot);

        result.Files.Should().ContainSingle();
        result.Directories.Should().BeEmpty();
        _directory.Verify(item => item.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [Fact]
    public void GIVEN_FileWithoutParentPath_WHEN_BuildingManifest_THEN_ShouldRetainFileOnly()
    {
        SetupFile("Workspace.sln");
        _path.Setup(item => item.GetDirectoryName("Workspace.sln")).Returns((string?)null);

        var result = BuildCertifiedManifest(_workspace.CurrentSolution, "Workspace.sln", "/Workspace");

        result.Files.Should().ContainSingle();
        result.Directories.Should().BeEmpty();
        _directoryInfoFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_FileWithWhitespaceParentPath_WHEN_BuildingManifest_THEN_ShouldRetainFileOnly()
    {
        SetupFile("Workspace.sln");
        _path.Setup(item => item.GetDirectoryName("Workspace.sln")).Returns(" ");

        var result = BuildCertifiedManifest(_workspace.CurrentSolution, "Workspace.sln", "/Workspace");

        result.Files.Should().ContainSingle();
        result.Directories.Should().BeEmpty();
        _directoryInfoFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ProjectAndDocumentWithoutPaths_WHEN_BuildingManifest_THEN_ShouldRetainOnlyLoadedPath()
    {
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp))
            .AddDocument(DocumentId.CreateNewId(projectId), "Document.cs", SourceText.From("class Document { }"));

        var workspaceRoot = Path.GetFullPath("/Workspace");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        SetupFile(solutionPath);
        SetupDirectory(workspaceRoot);
        _projectInputResolver.Setup(item => item.Resolve(null))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        var result = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);

        result.Files.Should().ContainSingle().Which.Path.Should().Be(solutionPath);
    }

    [Fact]
    public void GIVEN_ProjectInputEvaluationFailure_WHEN_BuildingManifest_THEN_ShouldRetainFailureAndReportChange()
    {
        var workspaceRoot = Path.GetFullPath("/Workspace");
        var projectPath = Path.Combine(workspaceRoot, "Project.csproj");
        var solutionPath = Path.Combine(workspaceRoot, "Workspace.sln");
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath));

        SetupFile(solutionPath);
        SetupFile(projectPath);
        SetupDirectory(workspaceRoot);
        _directory.Setup(item => item.EnumerateDirectories(workspaceRoot, "*", SearchOption.TopDirectoryOnly)).Returns([]);
        _projectInputResolver.Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Failed(projectPath, "Message"));

        var manifest = BuildCertifiedManifest(solution, solutionPath, workspaceRoot);
        var hasChanged = _target.HasChanged(manifest, TestContext.Current.CancellationToken);

        manifest.IsComplete.Should().BeFalse();
        manifest.EvaluationFailures.Should().ContainSingle().Which.Should().BeEquivalentTo(new WorkspaceProjectInputFailure
        {
            ProjectPath = projectPath,
            Message = "Message",
        });

        hasChanged.Should().BeTrue();
        manifest.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.ManifestValidation,
            Kind = WorkspaceInputChangeKind.ManifestIncomplete,
        });
    }

    [Fact]
    public void GIVEN_MatchingFingerprints_WHEN_CheckingForChanges_THEN_ShouldReportNoChange()
    {
        SetupDirectory("/Workspace");
        SetupFile("/Workspace/Document.cs", length: 10);
        using var manifest = CreateManifest();

        _target.HasChanged(manifest, TestContext.Current.CancellationToken).Should().BeFalse();
        manifest.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_DirectoryMetadataChanges_WHEN_CheckingForChanges_THEN_ShouldReportNoChange()
    {
        SetupDirectory("/Workspace", LastWriteTimeUtc.AddMinutes(1));
        SetupFile("/Workspace/Document.cs", length: 10);
        using var manifest = CreateManifest();

        _target.HasChanged(manifest, TestContext.Current.CancellationToken).Should().BeFalse();
        manifest.Change.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ChangeMonitorReportsChange_WHEN_CheckingForChanges_THEN_ShouldReportChangeWithoutPolling()
    {
        var changeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var expectedChange = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Changed,
            Path = "/Workspace/Document.cs",
        };

        changeMonitor.SetupGet(item => item.Change).Returns(expectedChange);
        using var manifest = new WorkspaceInputManifest
        {
            ChangeMonitor = changeMonitor.Object,
        };

        var result = _target.HasChanged(manifest, TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        manifest.Change.Should().BeSameAs(expectedChange);
        _directoryInfoFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
        _fileInfoFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("MissingDirectory")]
    [InlineData("MissingFile")]
    [InlineData("ChangedFileTimestamp")]
    [InlineData("ChangedFileLength")]
    public void GIVEN_ChangedFingerprint_WHEN_CheckingForChanges_THEN_ShouldReportChange(string change)
    {
        SetupDirectory("/Workspace", exists: change != "MissingDirectory");
        SetupFile(
            "/Workspace/Document.cs",
            change == "ChangedFileTimestamp" ? LastWriteTimeUtc.AddMinutes(1) : LastWriteTimeUtc,
            change == "ChangedFileLength" ? 11 : 10,
            change != "MissingFile");
        using var manifest = CreateManifest();

        _target.HasChanged(manifest, TestContext.Current.CancellationToken).Should().BeTrue();
        var expectedPath = change == "MissingDirectory"
            ? "/Workspace"
            : "/Workspace/Document.cs";
        var expectedKind = change is "MissingDirectory" or "MissingFile"
            ? WorkspaceInputChangeKind.Deleted
            : WorkspaceInputChangeKind.MetadataChanged;

        manifest.Change.Should().BeEquivalentTo(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = expectedKind,
            Path = expectedPath,
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_CancelledToken_WHEN_CheckingManifest_THEN_ShouldPropagateCancellation(bool hasDirectory)
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        using var manifest = new WorkspaceInputManifest
        {
            Directories = hasDirectory ? [CreateDirectoryFingerprint()] : [],
            Files = hasDirectory ? [] : [CreateFileFingerprint()],
        };

        var action = () => _target.HasChanged(manifest, cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceInputManifest BuildCertifiedManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties = null)
    {
        using var certification = _target.BeginCertification(workspaceRoot);
        return _target.BuildManifest(
            solution,
            loadedPath,
            workspaceRoot,
            certification,
            msBuildProperties,
            TestContext.Current.CancellationToken);
    }

    private void SetupFile(
        string path,
        DateTime? lastWriteTimeUtc = null,
        long length = 1,
        bool exists = true)
    {
        var file = new Mock<IFileInfo>();
        file.SetupGet(item => item.Exists).Returns(exists);
        file.SetupGet(item => item.FullName).Returns(path);
        file.SetupGet(item => item.LastWriteTimeUtc).Returns(lastWriteTimeUtc ?? LastWriteTimeUtc);
        file.SetupGet(item => item.Length).Returns(length);
        _fileInfoFactory.Setup(item => item.New(path)).Returns(file.Object);
    }

    private void SetupMissingFile(string path)
    {
        SetupFile(path, exists: false);
    }

    private void SetupDirectory(string path, DateTime? lastWriteTimeUtc = null, bool exists = true)
    {
        var directory = new Mock<IDirectoryInfo>();
        directory.SetupGet(item => item.Exists).Returns(exists);
        directory.SetupGet(item => item.FullName).Returns(path);
        directory.SetupGet(item => item.LastWriteTimeUtc).Returns(lastWriteTimeUtc ?? LastWriteTimeUtc);
        _directoryInfoFactory.Setup(item => item.New(path)).Returns(directory.Object);
    }

    private void SetupMissingDirectory(string path)
    {
        SetupDirectory(path, exists: false);
    }

    private static WorkspaceInputManifest CreateManifest()
    {
        return new WorkspaceInputManifest
        {
            Directories = [CreateDirectoryFingerprint()],
            Files = [CreateFileFingerprint()],
        };
    }

    private static WorkspaceInputDirectoryFingerprint CreateDirectoryFingerprint()
    {
        return new WorkspaceInputDirectoryFingerprint
        {
            Path = "/Workspace",
        };
    }

    private static WorkspaceInputFileFingerprint CreateFileFingerprint()
    {
        return new WorkspaceInputFileFingerprint
        {
            Path = "/Workspace/Document.cs",
            LastWriteTimeUtc = LastWriteTimeUtc,
            Length = 10,
        };
    }
}
