namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class InlineVariableToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        InlineVariableTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<InlineVariableRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "inline-variable"
                && metadata.Title == "Inline Variable"
                && metadata.Description == "Inlines a local variable through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<InlineVariableRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RemoveDeclarationIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnUnsupportedOptionRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest(removeDeclaration: false);
        var target = new InlineVariableTool();

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("UnsupportedOption");
        requestResolver.Verify(item => item.ResolveSymbolAsync<MutationProposal>(
            It.IsAny<SymbolSelector?>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<IToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SymbolResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var target = new InlineVariableTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Symbol, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotLocal_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var symbol = new Mock<ISymbol>();
        var target = new InlineVariableTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Symbol, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = symbol.Object,
            });

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocalSymbolHasNoSourceLocation_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var local = CreateLocalSymbol([]);
        var target = new InlineVariableTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Symbol, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = local.Object,
            });

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocalSourceLocationCannotBeProjected_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var location = RoslynTestFactory.CreateSourceLocation();
        var local = CreateLocalSymbol([location]);
        var target = new InlineVariableTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Symbol, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = local.Object,
            });
        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns((ResolvedLocation?)null);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocalSourceLocationCanBeProjected_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeAction()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var location = RoslynTestFactory.CreateSourceLocation();
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation(location, "Code.cs");
        var local = CreateLocalSymbol([location]);
        var target = new InlineVariableTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Symbol, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = local.Object,
            });
        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns(resolvedLocation);
        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider"
                    && replayRequest.Title == "Inline temporary variable"
                    && replayRequest.EquivalenceKey == "Inline_temporary_variable"
                    && replayRequest.Location != null),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider"
                && replayRequest.Title == "Inline temporary variable"
                && replayRequest.EquivalenceKey == "Inline_temporary_variable"
                && replayRequest.Location != null),
            CancellationToken.None), Times.Once);
    }

    private static Mock<ICodeActionMutationContext> CreateContext(Mock<IToolRequestResolver> requestResolver, Mock<IWorkspaceResolver> workspaceResolver)
    {
        var services = new Mock<IToolExecutionServices>();
        var context = new Mock<ICodeActionMutationContext>();

        services
            .Setup(item => item.RequestResolver)
            .Returns(requestResolver.Object);
        context
            .Setup(item => item.ToolExecutionServices)
            .Returns(services.Object);
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

    private static Mock<ILocalSymbol> CreateLocalSymbol(IReadOnlyList<Location> locations)
    {
        var symbol = new Mock<ILocalSymbol>();

        symbol
            .Setup(item => item.Locations)
            .Returns(System.Collections.Immutable.ImmutableArray.CreateRange(locations));

        return symbol;
    }
}
