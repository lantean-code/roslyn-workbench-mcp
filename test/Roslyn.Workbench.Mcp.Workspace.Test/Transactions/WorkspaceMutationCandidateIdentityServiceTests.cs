using System.Text;

using Roslyn.Workbench.Mcp.TestSupport.Workspace;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceMutationCandidateIdentityServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly Mock<IWorkspaceDocumentContentService> _documentContentService;
    private readonly WorkspaceMutationCandidateIdentityService _target;

    public WorkspaceMutationCandidateIdentityServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));

        _documentContentService = new Mock<IWorkspaceDocumentContentService>();
        _documentContentService
            .Setup(item => item.CreateAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Returns((Document document, CancellationToken cancellationToken) => WorkspaceDocumentContentTestFactory.CreateAsync(document, cancellationToken));

        _documentContentService
            .Setup(item => item.HasEquivalentContent(It.IsAny<WorkspaceDocumentContent>(), It.IsAny<WorkspaceDocumentContent>()))
            .Returns((WorkspaceDocumentContent expected, WorkspaceDocumentContent candidate) => WorkspaceDocumentContentTestFactory.HasEquivalentContent(expected, candidate));

        _target = new WorkspaceMutationCandidateIdentityService(
            _pathComparison.Object,
            _documentContentService.Object);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task GIVEN_AddedModifiedAndDeletedDocuments_WHEN_CreatingIdentity_THEN_ShouldDescribeOrderedSourceChanges()
    {
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: "/Workspace/Project/Project.csproj");

        _workspace.AddProject(projectInfo);
        var modifiedDocumentId = DocumentId.CreateNewId(projectId);
        var deletedDocumentId = DocumentId.CreateNewId(projectId);
        var modifiedText = SourceText.From("class Before { }", Encoding.UTF8);
        var deletedText = SourceText.From("class Deleted { }", Encoding.UTF8);
        var modifiedDocumentInfo = CreateDocumentInfo(modifiedDocumentId, "Modified.cs", "/Workspace/Project/Modified.cs", modifiedText);
        var deletedDocumentInfo = CreateDocumentInfo(deletedDocumentId, "Deleted.cs", "/Workspace/Project/Deleted.cs", deletedText);

        _workspace.AddDocument(modifiedDocumentInfo);
        _workspace.AddDocument(deletedDocumentInfo);
        var currentSolution = _workspace.CurrentSolution;
        var changedText = SourceText.From("class After { }", Encoding.UTF8);
        var candidateSolution = currentSolution.WithDocumentText(modifiedDocumentId, changedText);
        candidateSolution = candidateSolution.RemoveDocument(deletedDocumentId);
        var addedDocumentId = DocumentId.CreateNewId(projectId);
        var addedText = SourceText.From("class Added { }", Encoding.UTF8);
        var addedDocumentInfo = CreateDocumentInfo(addedDocumentId, "Added.cs", "/Workspace/Project/Added.cs", addedText);
        candidateSolution = candidateSolution.AddDocument(addedDocumentInfo);

        var result = await _target.CreateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Documents.Select(item => item.DocumentPath.Path).Should().Equal(
            "/Workspace/Project/Added.cs",
            "/Workspace/Project/Deleted.cs",
            "/Workspace/Project/Modified.cs");
        result.Documents.Select(item => item.ChangeKind).Should().Equal(
            WorkspaceMutationDocumentChangeKind.Added,
            WorkspaceMutationDocumentChangeKind.Deleted,
            WorkspaceMutationDocumentChangeKind.Modified);
        result.Documents.Should().OnlyContain(item => item.ProjectId == projectId.Id);
        result.Documents.Should().OnlyContain(item => item.EncodingName == Encoding.UTF8.WebName);
        result.Documents[0].ContentHash.Should().Be(Convert.ToHexString(addedText.GetContentHash().AsSpan()));
        result.Documents[1].ContentHash.Should().Be(Convert.ToHexString(deletedText.GetContentHash().AsSpan()));
        result.Documents[2].ContentHash.Should().Be(Convert.ToHexString(changedText.GetContentHash().AsSpan()));
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_CreatingIdentity_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.CreateAsync(
            _workspace.CurrentSolution,
            _workspace.CurrentSolution,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_RecreatedTextHasEquivalentContent_WHEN_CreatingIdentity_THEN_ShouldExcludeDocument()
    {
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: "/Workspace/Project/Project.csproj");

        _workspace.AddProject(projectInfo);
        var documentId = DocumentId.CreateNewId(projectId);
        var currentText = SourceText.From("class C { }", Encoding.UTF8);
        var documentInfo = CreateDocumentInfo(documentId, "Document.cs", "/Workspace/Project/Document.cs", currentText);

        _workspace.AddDocument(documentInfo);
        var currentSolution = _workspace.CurrentSolution;
        var recreatedText = SourceText.From("class C { }", Encoding.UTF8);
        var candidateSolution = currentSolution.WithDocumentText(documentId, recreatedText);

        var result = await _target.CreateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_EquivalentContentSerializesDifferently_WHEN_CreatingIdentity_THEN_ShouldIncludeDocument()
    {
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: "/Workspace/Project/Project.csproj");

        _workspace.AddProject(projectInfo);
        var documentId = DocumentId.CreateNewId(projectId);
        var contentBytes = Encoding.UTF8.GetBytes("class C { }");
        var currentText = SourceText.From(contentBytes, contentBytes.Length, Encoding.UTF8);
        var documentInfo = CreateDocumentInfo(documentId, "Document.cs", "/Workspace/Project/Document.cs", currentText);

        _workspace.AddDocument(documentInfo);
        var currentSolution = _workspace.CurrentSolution;
        var encodingWithoutPreamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var candidateText = SourceText.From(contentBytes, contentBytes.Length, encodingWithoutPreamble);
        var candidateSolution = currentSolution.WithDocumentText(documentId, candidateText);

        var result = await _target.CreateAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        currentText.ContentEquals(candidateText).Should().BeTrue();
        currentText.GetChecksum().Should().Equal(candidateText.GetChecksum());
        result.Documents.Should().ContainSingle();
        result.Documents[0].ChangeKind.Should().Be(WorkspaceMutationDocumentChangeKind.Modified);
        var candidateDocument = candidateSolution.GetDocument(documentId)
            ?? throw new InvalidOperationException("The candidate document was not available.");
        var candidateContent = await WorkspaceDocumentContentTestFactory.CreateAsync(candidateDocument, TestContext.Current.CancellationToken);

        result.Documents[0].SerializedBytesHash.Should().Be(candidateContent.SerializedBytesHash);
    }

    [Fact]
    public void GIVEN_EquivalentIdentityWithinMaximum_WHEN_Matching_THEN_ShouldReturnTrue()
    {
        var document = CreateDocumentIdentity("/Workspace/Project/Document.cs", "Checksum", isCaseSensitive: true);
        var expectedIdentity = new WorkspaceMutationCandidateIdentity { Documents = [document] };
        var candidateDocument = CreateDocumentIdentity("/Workspace/Project/Document.cs", "Checksum", isCaseSensitive: true);
        var candidateIdentity = new WorkspaceMutationCandidateIdentity { Documents = [candidateDocument] };
        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = expectedIdentity,
            MaximumChangedDocuments = 1,
        };

        var result = _target.MatchesPrecondition(precondition, candidateIdentity);

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_CaseEquivalentPathsOnCaseInsensitiveFileSystem_WHEN_Matching_THEN_ShouldReturnTrue()
    {
        var expectedDocument = CreateDocumentIdentity("C:\\Workspace\\Document.cs", "Checksum", isCaseSensitive: false);
        var expectedIdentity = new WorkspaceMutationCandidateIdentity { Documents = [expectedDocument] };
        var candidateDocument = CreateDocumentIdentity("c:\\workspace\\document.cs", "Checksum", isCaseSensitive: false);
        var candidateIdentity = new WorkspaceMutationCandidateIdentity { Documents = [candidateDocument] };
        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = expectedIdentity,
            MaximumChangedDocuments = 1,
        };

        var result = _target.MatchesPrecondition(precondition, candidateIdentity);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("ChangedChecksum", 1)]
    [InlineData("Checksum", 0)]
    public void GIVEN_ContentOrMaximumDiffers_WHEN_Matching_THEN_ShouldReturnFalse(
        string candidateChecksum,
        int maximumChangedDocuments)
    {
        var expectedDocument = CreateDocumentIdentity("/Workspace/Project/Document.cs", "Checksum", isCaseSensitive: true);
        var expectedIdentity = new WorkspaceMutationCandidateIdentity { Documents = [expectedDocument] };
        var candidateDocument = CreateDocumentIdentity("/Workspace/Project/Document.cs", candidateChecksum, isCaseSensitive: true);
        var candidateIdentity = new WorkspaceMutationCandidateIdentity { Documents = [candidateDocument] };
        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = expectedIdentity,
            MaximumChangedDocuments = maximumChangedDocuments,
        };

        var result = _target.MatchesPrecondition(precondition, candidateIdentity);

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DocumentSetDiffers_WHEN_Matching_THEN_ShouldReturnFalse()
    {
        var expectedDocument = CreateDocumentIdentity("/Workspace/Project/Document.cs", "Checksum", isCaseSensitive: true);
        var expectedIdentity = new WorkspaceMutationCandidateIdentity { Documents = [expectedDocument] };
        var candidateIdentity = new WorkspaceMutationCandidateIdentity { Documents = [] };
        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = expectedIdentity,
            MaximumChangedDocuments = 1,
        };

        var result = _target.MatchesPrecondition(precondition, candidateIdentity);

        result.Should().BeFalse();
    }

    private static DocumentInfo CreateDocumentInfo(
        DocumentId documentId,
        string name,
        string path,
        SourceText text)
    {
        var textAndVersion = TextAndVersion.Create(text, VersionStamp.Default);
        var loader = TextLoader.From(textAndVersion);
        return DocumentInfo.Create(documentId, name, loader: loader, filePath: path);
    }

    private static WorkspaceMutationDocumentIdentity CreateDocumentIdentity(
        string path,
        string checksum,
        bool isCaseSensitive)
    {
        var documentPath = new FileSystemPathKey(path, isCaseSensitive);
        var identity = new WorkspaceMutationDocumentIdentity
        {
            ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DocumentPath = documentPath,
            ChangeKind = WorkspaceMutationDocumentChangeKind.Modified,
            ContentHash = checksum,
            SerializedBytesHash = checksum,
            EncodingName = Encoding.UTF8.WebName,
        };

        return identity;
    }
}
