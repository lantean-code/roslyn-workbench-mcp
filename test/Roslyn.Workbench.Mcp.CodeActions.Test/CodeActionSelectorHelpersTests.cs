namespace Roslyn.Workbench.Mcp.CodeActions.Test;

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
    public async Task GIVEN_MissingSymbolSelector_WHEN_Resolving_THEN_ShouldRejectInvalidRequest()
    {
        var result = await _target.ResolveSymbolAsync<TestResponse>(
            null,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeTrue();
        result.Rejection!.Error!.Code.Should().Be("InvalidRequest");
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
        _workspaceResolver.Verify(item => item.ResolveSymbolAsync(
            It.IsAny<SymbolSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationSelectorHasSnapshotMismatch_WHEN_ResolvingSymbol_THEN_ShouldRejectConflict()
    {
        var selector = new SymbolSelector { Location = new LocationSelector() };
        var snapshot = new SnapshotPrecondition { WorkspaceEpoch = 1 };
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
            .ReturnsAsync(SelectorResolveResult<ISymbol>.Resolved(symbol.Object));

        var result = await _target.ResolveSymbolAsync<TestResponse>(
            selector,
            new SnapshotPrecondition { WorkspaceEpoch = 1 },
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeFalse();
        result.Value.Should().BeSameAs(symbol.Object);
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Theory]
    [InlineData(1, "SymbolNotFound")]
    [InlineData(2, "SymbolAmbiguous")]
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
    public void GIVEN_MissingResolvedLocation_WHEN_CreatingLocationSelector_THEN_ShouldReturnNull()
    {
        var result = _target.CreateLocationSelector(null);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_ResolvedLocationLacksRequiredData_WHEN_CreatingLocationSelector_THEN_ShouldReturnNull(bool omitDocument)
    {
        var location = new ResolvedLocation
        {
            Document = omitDocument ? null : new DocumentReference { DocumentId = "DocumentId" },
            Span = omitDocument ? new TextSpanRange { Start = 1, Length = 2 } : null,
        };

        var result = _target.CreateLocationSelector(location);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("DocumentId", "DocumentPath", "DocumentId", null)]
    [InlineData("", "DocumentPath", null, "DocumentPath")]
    public void GIVEN_ResolvedLocation_WHEN_CreatingLocationSelector_THEN_ShouldProjectDocumentIdentityAndSpan(
        string documentId,
        string documentPath,
        string? expectedDocumentId,
        string? expectedPath)
    {
        var location = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = documentId,
                Path = documentPath,
            },
            Span = new TextSpanRange
            {
                Start = 1,
                Length = 2,
            },
        };

        var result = _target.CreateLocationSelector(location);

        result.Should().BeEquivalentTo(new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    DocumentId = expectedDocumentId,
                    Path = expectedPath,
                },
                Start = 1,
                Length = 2,
            },
        });
    }

#pragma warning disable CA1812 // Response fixture is consumed through generic contract metadata.
    private sealed record TestResponse;
#pragma warning restore CA1812
}
