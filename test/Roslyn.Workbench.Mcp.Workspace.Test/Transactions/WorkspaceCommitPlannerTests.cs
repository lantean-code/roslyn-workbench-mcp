using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceCommitPlannerTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IFile> _file = new();
    private readonly Mock<IDirectory> _directory = new();
    private readonly Mock<IPath> _path = new();
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();
    private readonly WorkspaceCommitPlanner _target;

    public WorkspaceCommitPlannerTests()
    {
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetFullPath(It.IsAny<string>())).Returns((string value) => Path.GetFullPath(value));
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns((string value) => Path.GetDirectoryName(value));
        _path.Setup(item => item.GetRelativePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string root, string value) => Path.GetRelativePath(root, value));
        _path.SetupGet(item => item.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        _path.Setup(item => item.IsPathRooted(It.IsAny<string>())).Returns((string value) => Path.IsPathRooted(value));
        _directory.Setup(item => item.Exists(It.IsAny<string>())).Returns(true);
        _pathComparison.SetupGet(item => item.Comparer).Returns(StringComparer.Ordinal);
        _target = new WorkspaceCommitPlanner(_fileSystem.Object, _pathComparison.Object);
    }

    [Fact]
    public async Task GIVEN_CreateReplaceAndDelete_WHEN_Planning_THEN_ShouldCaptureExactBeforeAndAfterBytes()
    {
        var projectId = ProjectId.CreateNewId();
        var changedId = DocumentId.CreateNewId(projectId);
        var removedId = DocumentId.CreateNewId(projectId);
        var addedId = DocumentId.CreateNewId(projectId);
        var projectPath = Path.GetFullPath("/workspace/project/project.csproj");
        var changedPath = Path.GetFullPath("/workspace/project/changed.cs");
        var removedPath = Path.GetFullPath("/workspace/project/removed.cs");
        var addedPath = Path.GetFullPath("/workspace/project/added.cs");
        var baseline = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath))
            .AddDocument(changedId, "changed.cs", SourceText.From("old"), filePath: changedPath)
            .AddDocument(removedId, "removed.cs", SourceText.From("remove"), filePath: removedPath);
        var current = baseline
            .WithDocumentText(changedId, SourceText.From("new", Encoding.Unicode))
            .RemoveDocument(removedId)
            .AddDocument(addedId, "added.cs", SourceText.From("add", Encoding.UTF8), filePath: addedPath);
        _file.Setup(item => item.Exists(changedPath)).Returns(true);
        _file.Setup(item => item.Exists(removedPath)).Returns(true);
        _file.Setup(item => item.Exists(addedPath)).Returns(false);
        _file.Setup(item => item.ReadAllBytesAsync(changedPath, It.IsAny<CancellationToken>())).ReturnsAsync([1, 2]);
        _file.Setup(item => item.ReadAllBytesAsync(removedPath, It.IsAny<CancellationToken>())).ReturnsAsync([3, 4]);

        var plan = await _target.CreateAsync("commit", "/workspace/solution.slnx", "/workspace", baseline, current, TestContext.Current.CancellationToken);

        plan.Manifest.Entries.Select(entry => entry.Operation).Should().BeEquivalentTo([
            WorkspaceFileOperation.Replace,
            WorkspaceFileOperation.Create,
            WorkspaceFileOperation.Delete]);
        plan.Artifacts["backup/000000.bin"].ToArray().Should().Equal(1, 2);
        plan.Artifacts["staged/000000.bin"].ToArray().Should().Equal(Encode(Encoding.Unicode, "new"));
        plan.Artifacts["staged/000001.bin"].ToArray().Should().Equal(Encode(Encoding.UTF8, "add"));
        plan.Artifacts["backup/000002.bin"].ToArray().Should().Equal(3, 4);
    }

    [Fact]
    public async Task GIVEN_DuplicateCanonicalTargets_WHEN_Planning_THEN_ShouldRejectPlan()
    {
        var projectId = ProjectId.CreateNewId();
        var firstId = DocumentId.CreateNewId(projectId);
        var secondId = DocumentId.CreateNewId(projectId);
        var projectPath = Path.GetFullPath("/workspace/project/project.csproj");
        var targetPath = Path.GetFullPath("/workspace/project/shared.cs");
        var baseline = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath));
        var current = baseline
            .AddDocument(firstId, "first.cs", SourceText.From("first"), filePath: targetPath)
            .AddDocument(secondId, "second.cs", SourceText.From("second"), filePath: targetPath);

        var action = async () => await _target.CreateAsync("commit", "/workspace/solution.slnx", "/workspace", baseline, current, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Planning_THEN_ShouldPropagateCancellationBeforeReadingFiles()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.CreateAsync(
            "commit", "/workspace/solution.slnx", "/workspace", _workspace.CurrentSolution, _workspace.CurrentSolution, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_ChangedAddedAndRemovedDocumentsWithoutPaths_WHEN_Planning_THEN_ShouldIgnoreNonFileDocuments()
    {
        var projectId = ProjectId.CreateNewId();
        var changedId = DocumentId.CreateNewId(projectId);
        var removedId = DocumentId.CreateNewId(projectId);
        var addedId = DocumentId.CreateNewId(projectId);
        var baseline = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp))
            .AddDocument(changedId, "changed.cs", SourceText.From("before"))
            .AddDocument(removedId, "removed.cs", SourceText.From("removed"));
        var current = baseline.WithDocumentText(changedId, SourceText.From("after"))
            .RemoveDocument(removedId)
            .AddDocument(addedId, "added.cs", SourceText.From("added"));

        var plan = await _target.CreateAsync("commit", "/workspace/solution.slnx", "/workspace", baseline, current, TestContext.Current.CancellationToken);

        plan.Manifest.Entries.Should().BeEmpty();
        plan.Artifacts.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_TargetExistenceChanged_WHEN_PlanningCreateOrDelete_THEN_ShouldRejectPlan(bool creating)
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectPath = Path.GetFullPath("/workspace/project/project.csproj");
        var targetPath = Path.GetFullPath("/workspace/project/target.cs");
        var project = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath));
        var baseline = creating ? project : project.AddDocument(documentId, "target.cs", SourceText.From("text"), filePath: targetPath);
        var current = creating
            ? baseline.AddDocument(documentId, "target.cs", SourceText.From("text"), filePath: targetPath)
            : baseline.RemoveDocument(documentId);
        _file.Setup(item => item.Exists(targetPath)).Returns(creating);

        var action = async () => await _target.CreateAsync("commit", "/workspace/solution.slnx", "/workspace", baseline, current, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task GIVEN_NewDocumentOutsideProjectBoundary_WHEN_Planning_THEN_ShouldRejectPlan()
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectPath = Path.GetFullPath("/workspace/project/project.csproj");
        var outsidePath = Path.GetFullPath("/outside/added.cs");
        var baseline = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath));
        var current = baseline.AddDocument(documentId, "added.cs", SourceText.From("text"), filePath: outsidePath);

        var action = async () => await _target.CreateAsync("commit", "/workspace/solution.slnx", "/workspace", baseline, current, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_TargetInsideProjectButOutsideWorkspaceRoot_WHEN_Planning_THEN_ShouldRejectPlan()
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectPath = Path.GetFullPath("/workspace/project/project.csproj");
        var targetPath = Path.GetFullPath("/workspace/project/added.cs");
        var baseline = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath));
        var current = baseline.AddDocument(documentId, "added.cs", SourceText.From("text"), filePath: targetPath);

        var action = async () => await _target.CreateAsync(
            "commit",
            "/workspace/solution.slnx",
            "/workspace/other",
            baseline,
            current,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_NewNestedDocument_WHEN_Planning_THEN_ShouldRecordMissingDirectoriesShallowToDeep()
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectDirectory = Path.GetFullPath("/workspace/project");
        var projectPath = Path.Combine(projectDirectory, "project.csproj");
        var firstDirectory = Path.Combine(projectDirectory, "generated");
        var secondDirectory = Path.Combine(firstDirectory, "nested");
        var targetPath = Path.Combine(secondDirectory, "added.cs");
        var baseline = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp, filePath: projectPath));
        var current = baseline.AddDocument(documentId, "added.cs", SourceText.From("text"), filePath: targetPath);
        _directory.Setup(item => item.Exists(firstDirectory)).Returns(false);
        _directory.Setup(item => item.Exists(secondDirectory)).Returns(false);
        _directory.Setup(item => item.Exists(projectDirectory)).Returns(true);

        var plan = await _target.CreateAsync("commit", "/workspace/solution.slnx", "/workspace", baseline, current, TestContext.Current.CancellationToken);

        plan.Manifest.CreatedDirectories.Should().Equal(firstDirectory, secondDirectory);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private static byte[] Encode(Encoding encoding, string text)
    {
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }
}
