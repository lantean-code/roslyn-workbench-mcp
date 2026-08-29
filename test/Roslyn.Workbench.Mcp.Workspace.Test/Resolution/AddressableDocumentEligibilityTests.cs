namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

public sealed class AddressableDocumentEligibilityTests : IDisposable
{
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly AddressableDocumentEligibility _target;
    private readonly AdhocWorkspace _workspace;

    public AddressableDocumentEligibilityTests()
    {
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);
        _target = new AddressableDocumentEligibility(_pathComparison.Object);
        _workspace = new AdhocWorkspace();
    }

    [Theory]
    [InlineData("/workspace/src/Document.cs", true)]
    [InlineData("/workspace/objects/Document.cs", true)]
    [InlineData("/workspace/obj/Document.cs", false)]
    [InlineData(@"C:\workspace\obj\Document.cs", false)]
    public void GIVEN_DocumentPath_WHEN_CheckingAgentAddressability_THEN_ShouldReturnExpectedResult(string path, bool expected)
    {
        var document = CreateDocument(path);

        var result = _target.IsAddressable(document);

        result.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_DocumentWithoutPhysicalPath_WHEN_CheckingAgentAddressability_THEN_ShouldReturnTrue()
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var document = _workspace.AddDocument(project.Id, "Document.cs", SourceText.From("class C { }"));

        var result = _target.IsAddressable(document);

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_CaseInsensitivePathComparison_WHEN_ObjSegmentUsesDifferentCase_THEN_ShouldReturnFalse()
    {
        var path = "/workspace/OBJ/Document.cs";
        _pathComparison
            .Setup(item => item.GetComparison(path))
            .Returns(StringComparison.OrdinalIgnoreCase);
        var document = CreateDocument(path);

        var result = _target.IsAddressable(document);

        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private Document CreateDocument(string path)
    {
        var project = _workspace.CurrentSolution.Projects.SingleOrDefault()
            ?? _workspace.AddProject("Project", LanguageNames.CSharp);

        var documentInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            Path.GetFileName(path),
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("class C { }"), VersionStamp.Default)),
            filePath: path);

        return _workspace.AddDocument(documentInfo);
    }
}
