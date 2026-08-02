using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

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
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns((string path) => Path.GetDirectoryName(path));
        _path.Setup(item => item.GetFileName(It.IsAny<string>())).Returns((string path) => Path.GetFileName(path));
        _path.Setup(item => item.TrimEndingDirectorySeparator(It.IsAny<string>())).Returns((string path) => Path.TrimEndingDirectorySeparator(path));
        _path.Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string left, string right) => Path.Combine(left, right));
        _changeMonitorFactory
            .Setup(item => item.Create(It.IsAny<string>()))
            .Returns(_changeMonitor.Object);
        _pathComparison
            .Setup(item => item.GetComparer(It.IsAny<string>()))
            .Returns(StringComparer.Ordinal);
        _pathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);

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
        var action = () => _target.BuildManifest(_workspace.CurrentSolution, loadedPath, "/Workspace");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_InMemorySolutionWithWorkspaceInputs_WHEN_BuildingManifest_THEN_ShouldCaptureDistinctTrackedInputs()
    {
        const string root = "/Workspace";
        const string projectPath = root + "/Project/Project.csproj";
        const string sourcePath = root + "/Project/Document.cs";
        const string additionalPath = root + "/Project/Additional.txt";
        const string editorConfigPath = root + "/Project/.editorconfig";
        const string generatedPath = root + "/Project/obj/Debug/Generated.cs";
        const string analyzerPath = root + "/Analyzers/Analyzer.dll";
        var referencePath = typeof(object).Assembly.Location;
        const string importPath = root + "/Build/Imported.props";
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

        foreach (var filePath in new[] { root + "/Workspace.sln", projectPath, sourcePath, additionalPath, editorConfigPath, analyzerPath, referencePath, importPath })
        {
            SetupFile(filePath);
            SetupDirectory(Path.GetDirectoryName(filePath)!);
        }

        SetupDirectory(root + "/Project/Source");
        _projectInputResolver.Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded([importPath]));

        _directory.Setup(item => item.EnumerateDirectories(root + "/Project", "*", SearchOption.AllDirectories)).Returns(
            [root + "/Project/Source", root + "/Project/bin", root + "/Project/obj"]);

        var result = _target.BuildManifest(solution, root + "/Workspace.sln", root);

        result.Files.Select(item => item.Path).Should().BeEquivalentTo(
            root + "/Workspace.sln",
            projectPath,
            sourcePath,
            additionalPath,
            editorConfigPath,
            analyzerPath,
            referencePath,
            importPath);

        result.Directories.Select(item => item.Path).Should().Contain(root + "/Project/Source");
        result.Directories.Select(item => item.Path).Should().NotContain(root + "/Project/bin", root + "/Project/obj");
        result.Files.Select(item => item.Path).Should().NotContain(generatedPath);
        _changeMonitorFactory.Verify(item => item.Create(root), Times.Once);
        _changeMonitor.Verify(item => item.Start(), Times.Once);
        _changeMonitor.Verify(item => item.Track(result), Times.Once);
    }

    [Fact]
    public void GIVEN_ManifestConstructionFailure_WHEN_BuildingManifest_THEN_ShouldDisposeChangeMonitor()
    {
        _fileInfoFactory
            .Setup(item => item.New("/Workspace/Workspace.sln"))
            .Throws(new IOException());

        var action = () => _target.BuildManifest(
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
        var ignoredPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            directoryPath,
            filePath,
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
        const string root = "/Workspace";
        const string projectPath = root + "/Project/Project.csproj";
        const string conventionalNamedSourcePath = root + "/Project/obj/Source.cs";
        const string generatedPath = root + "/Project/custom-obj/Generated.cs";
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

        foreach (var filePath in new[] { root + "/Workspace.sln", projectPath, conventionalNamedSourcePath, generatedPath })
        {
            SetupFile(filePath);
            SetupDirectory(Path.GetDirectoryName(filePath)!);
        }

        _directory
            .Setup(item => item.EnumerateDirectories(root + "/Project", "*", SearchOption.AllDirectories))
            .Returns([root + "/Project/obj", root + "/Project/custom-obj"]);
        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded(
                artifactRoots: [root + "/Project/custom-bin", root + "/Project/custom-obj"]));

        var result = _target.BuildManifest(solution, root + "/Workspace.sln", root);

        result.Files.Select(static file => file.Path).Should().Contain(conventionalNamedSourcePath);
        result.Files.Select(static file => file.Path).Should().NotContain(generatedPath);
        result.Directories.Select(static directory => directory.Path).Should().Contain(root + "/Project/obj");
        result.Directories.Select(static directory => directory.Path).Should().NotContain(root + "/Project/custom-obj");
    }

    [Fact]
    public void GIVEN_MultipleTargetFrameworkProjects_WHEN_BuildingManifest_THEN_ShouldEvaluatePhysicalProjectOnce()
    {
        const string root = "/Workspace";
        const string projectPath = root + "/Project/Project.csproj";
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

        SetupFile(root + "/Workspace.sln");
        SetupFile(projectPath);
        SetupDirectory(root);
        SetupDirectory(root + "/Project");
        _directory
            .Setup(item => item.EnumerateDirectories(root + "/Project", "*", SearchOption.AllDirectories))
            .Returns([]);
        _projectInputResolver
            .Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded(
                artifactRoots: [root + "/Project/bin", root + "/Project/obj"]));

        using var result = _target.BuildManifest(solution, root + "/Workspace.sln", root);

        result.IsComplete.Should().BeTrue();
        _projectInputResolver.Verify(item => item.Resolve(projectPath), Times.Once);
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

        var result = _target.BuildManifest(solution, "/Missing/Workspace.sln", "/Missing");

        result.Files.Should().BeEmpty();
        result.Directories.Should().BeEmpty();
        _directory.Verify(item => item.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ProjectWithoutExistingDirectory_WHEN_BuildingManifest_THEN_ShouldNotEnumerateDirectories()
    {
        const string projectPath = "/Workspace/Project/Project.csproj";
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath));

        SetupMissingFile("/Missing/Workspace.sln");
        SetupFile(projectPath);
        SetupMissingDirectory("/Workspace/Project");
        _projectInputResolver.Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        var result = _target.BuildManifest(solution, "/Missing/Workspace.sln", "/Missing");

        result.Files.Should().ContainSingle();
        result.Directories.Should().BeEmpty();
        _directory.Verify(item => item.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [Fact]
    public void GIVEN_FileWithoutParentPath_WHEN_BuildingManifest_THEN_ShouldRetainFileOnly()
    {
        SetupFile("Workspace.sln");
        _path.Setup(item => item.GetDirectoryName("Workspace.sln")).Returns((string?)null);

        var result = _target.BuildManifest(_workspace.CurrentSolution, "Workspace.sln", "/Workspace");

        result.Files.Should().ContainSingle();
        result.Directories.Should().BeEmpty();
        _directoryInfoFactory.Verify(item => item.New(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_FileWithWhitespaceParentPath_WHEN_BuildingManifest_THEN_ShouldRetainFileOnly()
    {
        SetupFile("Workspace.sln");
        _path.Setup(item => item.GetDirectoryName("Workspace.sln")).Returns(" ");

        var result = _target.BuildManifest(_workspace.CurrentSolution, "Workspace.sln", "/Workspace");

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

        SetupFile("/Workspace/Workspace.sln");
        SetupDirectory("/Workspace");
        _projectInputResolver.Setup(item => item.Resolve(null))
            .Returns(WorkspaceProjectInputResolution.Succeeded());

        var result = _target.BuildManifest(solution, "/Workspace/Workspace.sln", "/Workspace");

        result.Files.Should().ContainSingle().Which.Path.Should().Be("/Workspace/Workspace.sln");
    }

    [Fact]
    public void GIVEN_ProjectInputEvaluationFailure_WHEN_BuildingManifest_THEN_ShouldRetainFailureAndReportChange()
    {
        const string projectPath = "/Workspace/Project.csproj";
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath));

        SetupFile("/Workspace/Workspace.sln");
        SetupFile(projectPath);
        SetupDirectory("/Workspace");
        _directory.Setup(item => item.EnumerateDirectories("/Workspace", "*", SearchOption.AllDirectories)).Returns([]);
        _projectInputResolver.Setup(item => item.Resolve(projectPath))
            .Returns(WorkspaceProjectInputResolution.Failed(projectPath, "Message"));

        var manifest = _target.BuildManifest(solution, "/Workspace/Workspace.sln", "/Workspace");
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
