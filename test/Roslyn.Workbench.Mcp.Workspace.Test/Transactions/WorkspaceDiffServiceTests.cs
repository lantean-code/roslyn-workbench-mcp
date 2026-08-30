namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceDiffServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceResolver> _resolver;
    private readonly WorkspaceDiffService _target;

    public WorkspaceDiffServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _resolver = new Mock<IWorkspaceResolver>();
        _target = new WorkspaceDiffService();
    }

    [Fact]
    public async Task GIVEN_UnchangedSolution_WHEN_CreatingChangeSummary_THEN_ShouldDelegateToDiffBuilder()
    {
        var solution = _workspace.CurrentSolution;

        var result = await _target.CreateChangeSummaryAsync(
            solution,
            solution,
            _resolver.Object,
            TestContext.Current.CancellationToken);

        result.Added.Should().BeEmpty();
        result.Modified.Should().BeEmpty();
        result.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_MissingDocument_WHEN_CreatingDocumentDiff_THEN_ShouldDelegateToDiffBuilder()
    {
        var solution = _workspace.CurrentSolution;
        var reference = new DocumentReference { DocumentId = "DocumentId", Path = "Path", ProjectId = "ProjectId" };
        _resolver.Setup(item => item.ResolveDocument(It.Is<DocumentSelector>(selector => selector.DocumentId == "DocumentId")))
            .Returns(SelectorResolveResult.NotFound<Document>());

        var result = await _target.CreateDocumentDiffAsync(
            solution,
            solution,
            reference,
            _resolver.Object,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
