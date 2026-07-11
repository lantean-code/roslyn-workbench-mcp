using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceCommitWriterTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IPath> _path;
    private readonly WorkspaceCommitWriter _target;

    public WorkspaceCommitWriterTests()
    {
        _workspace = new AdhocWorkspace();
        _fileSystem = new Mock<IFileSystem>();
        _file = new Mock<IFile>();
        _directory = new Mock<IDirectory>();
        _path = new Mock<IPath>();
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _fileSystem.SetupGet(item => item.Directory).Returns(_directory.Object);
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path.Setup(item => item.GetDirectoryName(It.IsAny<string>())).Returns("/Workspace");
        _target = new WorkspaceCommitWriter(_fileSystem.Object);
    }

    [Fact]
    public async Task GIVEN_ChangedAndAddedDocuments_WHEN_Applying_THEN_ShouldCreateDirectoriesAndWriteCurrentText()
    {
        var projectId = ProjectId.CreateNewId();
        var changedDocumentId = DocumentId.CreateNewId(projectId);
        var addedDocumentId = DocumentId.CreateNewId(projectId);
        var baseline = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp))
            .AddDocument(changedDocumentId, "Changed.cs", SourceText.From("Before"), filePath: "/Workspace/Changed.cs");
        var current = baseline
            .WithDocumentText(changedDocumentId, SourceText.From("After"))
            .AddDocument(addedDocumentId, "Added.cs", SourceText.From("Added"), filePath: "/Workspace/Added.cs");

        await _target.ApplyAsync(baseline, current, TestContext.Current.CancellationToken);

        _directory.Verify(item => item.CreateDirectory("/Workspace"), Times.Exactly(2));
        _file.Verify(item => item.WriteAllTextAsync(
            "/Workspace/Changed.cs",
            "After",
            It.IsAny<Encoding>(),
            TestContext.Current.CancellationToken), Times.Once);
        _file.Verify(item => item.WriteAllTextAsync(
            "/Workspace/Added.cs",
            "Added",
            It.IsAny<Encoding>(),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DocumentsWithoutPaths_WHEN_Applying_THEN_ShouldIgnoreTheirFileOperations()
    {
        var projectId = ProjectId.CreateNewId();
        var changedDocumentId = DocumentId.CreateNewId(projectId);
        var removedDocumentId = DocumentId.CreateNewId(projectId);
        var addedDocumentId = DocumentId.CreateNewId(projectId);
        var baseline = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp))
            .AddDocument(changedDocumentId, "Changed.cs", SourceText.From("Before"))
            .AddDocument(removedDocumentId, "Removed.cs", SourceText.From("Removed"));
        var current = baseline
            .WithDocumentText(changedDocumentId, SourceText.From("After"))
            .RemoveDocument(removedDocumentId)
            .AddDocument(addedDocumentId, "Added.cs", SourceText.From("Added"));

        await _target.ApplyAsync(baseline, current, TestContext.Current.CancellationToken);

        _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
        _file.Verify(item => item.WriteAllTextAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Encoding>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _file.Verify(item => item.Delete(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_RemovedDocument_WHEN_Applying_THEN_ShouldDeleteOnlyExistingFile(bool fileExists)
    {
        var projectId = ProjectId.CreateNewId();
        var removedDocumentId = DocumentId.CreateNewId(projectId);
        var baseline = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp))
            .AddDocument(removedDocumentId, "Removed.cs", SourceText.From("Removed"), filePath: "/Workspace/Removed.cs");
        var current = baseline.RemoveDocument(removedDocumentId);
        _file.Setup(item => item.Exists("/Workspace/Removed.cs")).Returns(fileExists);

        await _target.ApplyAsync(baseline, current, TestContext.Current.CancellationToken);

        _file.Verify(
            item => item.Delete("/Workspace/Removed.cs"),
            fileExists ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ApplyingChanges_THEN_ShouldPropagateCancellation()
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var baseline = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Project",
            "Project",
            LanguageNames.CSharp));
        var current = baseline.AddDocument(documentId, "Added.cs", SourceText.From("Added"), filePath: "/Workspace/Added.cs");
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.ApplyAsync(baseline, current, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _directory.Verify(item => item.CreateDirectory(It.IsAny<string>()), Times.Never);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
