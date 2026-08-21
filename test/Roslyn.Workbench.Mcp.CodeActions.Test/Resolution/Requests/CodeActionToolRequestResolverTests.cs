using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Resolution.Requests;

public sealed class CodeActionToolRequestResolverTests
{
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly CodeActionToolRequestResolver _target;

    public CodeActionToolRequestResolverTests()
    {
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _context = new Mock<ICodeActionExecutionContext>();
        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _target = new CodeActionToolRequestResolver(new CodeActionScopeResolver());
    }

    [Fact]
    public async Task GIVEN_LocationSelectorHasSnapshotMismatch_WHEN_ResolvingSymbol_THEN_ShouldRejectConflict()
    {
        var selector = new SymbolSelector { Location = new LocationSelector() };
        var snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(snapshot))
            .Returns(SnapshotMatchResult.WorkspaceEpochMismatch());

        var result = await _target.ResolveSymbolAsync<TestResponse>(
            selector,
            snapshot,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeTrue();
        result.Rejection!.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Rejection.Error!.Code.Should().Be("SnapshotMismatch");
        _workspaceResolver.Verify(item => item.ResolveSymbolAsync(
            It.IsAny<SymbolSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentationSelectorResolves_WHEN_ResolvingSymbol_THEN_ShouldReturnSymbolWithoutSnapshotValidation()
    {
        var selector = new SymbolSelector { DocumentationCommentId = "DocumentationCommentId" };
        var symbol = new Mock<ISymbol>();
        _workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(selector, TestContext.Current.CancellationToken))
            .ReturnsAsync(SelectorResolveResult.Resolved(symbol.Object));

        var result = await _target.ResolveSymbolAsync<TestResponse>(
            selector,
            WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeFalse();
        result.Value.Should().BeSameAs(symbol.Object);
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Theory]
    [InlineData(1, "SymbolNotFound")]
    [InlineData(2, "SymbolAmbiguous")]
    [InlineData(3, "SymbolSelectorInvalid")]
    public async Task GIVEN_SymbolDoesNotResolve_WHEN_Resolving_THEN_ShouldMapResolutionStatus(
        int statusValue,
        string expectedCode)
    {
        var selector = new SymbolSelector { DocumentationCommentId = "DocumentationCommentId" };
        var status = (SelectorResolveStatus)statusValue;
        _workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(selector, TestContext.Current.CancellationToken))
            .ReturnsAsync(SelectorTestFactory.CreateUnresolvedResult<ISymbol>(status));

        var result = await _target.ResolveSymbolAsync<TestResponse>(
            selector,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeTrue();
        result.Rejection!.Error!.Code.Should().Be(expectedCode);
        result.Rejection.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutRange_WHEN_ResolvingSelection_THEN_ShouldReturnCompleteDocumentSpan()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new DocumentSelector { Path = "Code.cs" };
        _workspaceResolver
            .Setup(item => item.ResolveDocument(selector))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document));

        var result = await _target.ResolveDocumentSelectionAsync<TestResponse>(
            selector,
            range: null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeFalse();
        var selection = result.Value ?? throw new InvalidOperationException("The document selection was not resolved.");
        selection.Document.Should().BeSameAs(roslyn.Document);
        selection.Span.Should().Be(new TextSpan(0, 11));
    }

    [Fact]
    public async Task GIVEN_DocumentWithRange_WHEN_ResolvingSelection_THEN_ShouldReturnExactSpan()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new DocumentSelector { Path = "Code.cs" };
        _workspaceResolver
            .Setup(item => item.ResolveDocument(selector))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document));

        var result = await _target.ResolveDocumentSelectionAsync<TestResponse>(
            selector,
            new TextSpanRange { Start = 2, Length = 3 },
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeFalse();
        var selection = result.Value ?? throw new InvalidOperationException("The document selection was not resolved.");
        selection.Span.Should().Be(new TextSpan(2, 3));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(12, 0)]
    [InlineData(10, 2)]
    public async Task GIVEN_InvalidRange_WHEN_ResolvingDocumentSelection_THEN_ShouldRejectRange(
        int start,
        int length)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new DocumentSelector { Path = "Code.cs" };
        _workspaceResolver
            .Setup(item => item.ResolveDocument(selector))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document));

        var result = await _target.ResolveDocumentSelectionAsync<TestResponse>(
            selector,
            new TextSpanRange { Start = start, Length = length },
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeTrue();
        result.Rejection!.Error!.Code.Should().Be("InvalidRange");
    }

#pragma warning disable CA1812 // Response fixture is consumed through generic contract metadata.
    private sealed record TestResponse;
#pragma warning restore CA1812
}
