namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class GeneratedSourceMutationInspectorTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IGeneratedSourceClassifier> _classifier;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IPath> _path;
    private readonly GeneratedSourceMutationInspector _target;

    public GeneratedSourceMutationInspectorTests()
    {
        _workspace = new AdhocWorkspace();
        _classifier = new Mock<IGeneratedSourceClassifier>();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _fileSystem = new Mock<IFileSystem>();
        _path = new Mock<IPath>();
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));

        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path
            .Setup(item => item.GetRelativePath("/workspace", It.IsAny<string>()))
            .Returns((string _, string path) => path["/workspace/".Length..].Replace('/', '\\'));

        _target = new GeneratedSourceMutationInspector(
            _classifier.Object,
            _pathComparison.Object,
            _fileSystem.Object);
    }

    [Fact]
    public async Task GIVEN_AddedGeneratedAndOrdinaryDocuments_WHEN_Inspecting_THEN_ShouldReturnOnlyGeneratedPath()
    {
        var project = _workspace.CurrentSolution.AddProject("Project", "Project", LanguageNames.CSharp);
        var baseline = project.Solution;
        var generatedDocument = project.AddDocument(
            "Generated.g.cs",
            SourceText.From("class Generated { }"),
            filePath: "/workspace/Generated.g.cs");

        var candidate = generatedDocument.Project.Solution.AddDocument(
            DocumentId.CreateNewId(project.Id),
            "Ordinary.cs",
            SourceText.From("class Ordinary { }"),
            filePath: "/workspace/Ordinary.cs");

        _classifier
            .Setup(item => item.IsGeneratedLookingAsync(
                It.Is<Document>(document => document.Name == "Generated.g.cs"),
                "/workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _target.FindGeneratedSourcePathsAsync(
            baseline,
            candidate,
            "/workspace",
            TestContext.Current.CancellationToken);

        result.Should().Equal("Generated.g.cs");
    }

    [Fact]
    public async Task GIVEN_ChangedAndRemovedGeneratedDocuments_WHEN_Inspecting_THEN_ShouldReturnSortedPaths()
    {
        var project = _workspace.CurrentSolution.AddProject("Project", "Project", LanguageNames.CSharp);
        var changedId = DocumentId.CreateNewId(project.Id);
        var removedId = DocumentId.CreateNewId(project.Id);
        var baseline = project.Solution
            .AddDocument(changedId, "Z.g.cs", SourceText.From("class Z { }"), filePath: "/workspace/Z.g.cs")
            .AddDocument(removedId, "A.g.cs", SourceText.From("class A { }"), filePath: "/workspace/A.g.cs");

        var candidate = baseline
            .WithDocumentText(changedId, SourceText.From("class Z { int Value; }"))
            .RemoveDocument(removedId);

        _classifier
            .Setup(item => item.IsGeneratedLookingAsync(
                It.IsAny<Document>(),
                "/workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _target.FindGeneratedSourcePathsAsync(
            baseline,
            candidate,
            "/workspace",
            TestContext.Current.CancellationToken);

        result.Should().Equal("A.g.cs", "Z.g.cs");
    }

    [Fact]
    public async Task GIVEN_LinkedGeneratedDocuments_WHEN_Inspecting_THEN_ShouldClassifyPhysicalPathOnce()
    {
        var firstProject = _workspace.CurrentSolution.AddProject("First", "First", LanguageNames.CSharp);
        var secondProject = firstProject.Solution.AddProject("Second", "Second", LanguageNames.CSharp);
        var baseline = secondProject.Solution;
        var candidate = baseline
            .AddDocument(DocumentId.CreateNewId(firstProject.Id), "Shared.g.cs", SourceText.From("class Shared { }"), filePath: "/workspace/Shared.g.cs")
            .AddDocument(DocumentId.CreateNewId(secondProject.Id), "Shared.g.cs", SourceText.From("class Shared { }"), filePath: "/workspace/Shared.g.cs");

        _classifier
            .Setup(item => item.IsGeneratedLookingAsync(
                It.IsAny<Document>(),
                "/workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _target.FindGeneratedSourcePathsAsync(
            baseline,
            candidate,
            "/workspace",
            TestContext.Current.CancellationToken);

        result.Should().Equal("Shared.g.cs");
        _classifier.Verify(item => item.IsGeneratedLookingAsync(
            It.IsAny<Document>(),
            "/workspace",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PathlessChangedDocument_WHEN_Inspecting_THEN_ShouldIgnoreDocument()
    {
        var project = _workspace.CurrentSolution.AddProject("Project", "Project", LanguageNames.CSharp);
        var baseline = project.Solution;
        var candidate = project.AddDocument("Generated.g.cs", SourceText.From("class Generated { }")).Project.Solution;

        var result = await _target.FindGeneratedSourcePathsAsync(
            baseline,
            candidate,
            "/workspace",
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _classifier.Verify(item => item.IsGeneratedLookingAsync(
            It.IsAny<Document>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
