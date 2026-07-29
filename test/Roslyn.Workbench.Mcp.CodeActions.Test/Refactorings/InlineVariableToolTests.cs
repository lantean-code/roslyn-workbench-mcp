namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class InlineVariableToolTests
{
    private readonly Mock<IWorkspaceSelectorFactory> _selectorFactory;

    public InlineVariableToolTests()
    {
        _selectorFactory = new Mock<IWorkspaceSelectorFactory>();
    }

    [Fact]
    public async Task GIVEN_RemoveDeclarationIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnUnsupportedOptionRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest(removeDeclaration: false);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("UnsupportedOption");
        workspaceResolver.Verify(item => item.ResolveSymbolAsync(
            It.IsAny<SymbolSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);

        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SymbolResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(new CodeActionExecutionError
        {
            Code = "SymbolNotFound",
            Message = "The symbol selector did not match any result.",
        }, RequiredAction.ResolveTargetAgain);

        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Symbol!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.NotFound<ISymbol>());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotLocal_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var symbol = new Mock<ISymbol>();
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Symbol!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(symbol.Object));

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocalSymbolHasNoSourceLocation_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var local = CreateLocalSymbol([]);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Symbol!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(local.Object));

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocalSourceLocationCannotBeProjected_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var location = RoslynTestFactory.CreateSourceLocation();
        var local = CreateLocalSymbol([location]);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Symbol!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(local.Object));

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns((ResolvedLocation?)null);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocalSourceLocationCanBeProjected_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeAction()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var location = RoslynTestFactory.CreateSourceLocation();
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation(location, "Code.cs");
        var local = CreateLocalSymbol([location]);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Symbol!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(local.Object));

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns(resolvedLocation);

        _selectorFactory
            .Setup(item => item.CreateLocationSelector(resolvedLocation))
            .Returns(new LocationSelector());

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider"
                    && replayRequest.Title == "Inline temporary variable"
                    && replayRequest.EquivalenceKey == "Inline_temporary_variable"
                    && replayRequest.Location != null),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider"
                && replayRequest.Title == "Inline temporary variable"
                && replayRequest.EquivalenceKey == "Inline_temporary_variable"
                && replayRequest.Location != null),
            context.Object, CancellationToken.None), Times.Once);
    }

    private static Mock<ICodeActionMutationContext> CreateContext(Mock<IWorkspaceResolver> workspaceResolver)
    {
        var context = new Mock<ICodeActionMutationContext>();
        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        return context;
    }

    private static InlineVariableRequest CreateRequest(bool removeDeclaration = true)
    {
        return new InlineVariableRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "DocumentationCommentId",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            RemoveDeclaration = removeDeclaration,
        };
    }

    private InlineVariableTool CreateTarget(ICodeActionSelectionStager selectionStager)
    {
        var requestResolver = new CodeActionToolRequestResolver(new CodeActionScopeResolver());

        return new InlineVariableTool(selectionStager, requestResolver, _selectorFactory.Object);
    }

    private static Mock<ILocalSymbol> CreateLocalSymbol(IReadOnlyList<Location> locations)
    {
        var symbol = new Mock<ILocalSymbol>();

        symbol
            .Setup(item => item.Locations)
            .Returns(System.Collections.Immutable.ImmutableArray.CreateRange(locations));

        return symbol;
    }
}
