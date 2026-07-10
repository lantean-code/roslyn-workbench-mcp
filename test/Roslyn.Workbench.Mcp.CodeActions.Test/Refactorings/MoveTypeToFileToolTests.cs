namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class MoveTypeToFileToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        MoveTypeToFileTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<MoveTypeToFileRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "move-type-to-file"
                && metadata.Title == "Move Type To File"
                && metadata.Description == "Moves one selected type into its own Roslyn-chosen file within the current project."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<MoveTypeToFileRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PreserveNamespaceIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnUnsupportedOptionRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest(preserveNamespace: false);
        var target = new MoveTypeToFileTool();

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
        var target = new MoveTypeToFileTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Type, request.ExpectedSnapshot, context.Object, CancellationToken.None))
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
    public async Task GIVEN_ResolvedSymbolIsNotNamedType_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var symbol = new Mock<ISymbol>();
        var target = new MoveTypeToFileTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Type, request.ExpectedSnapshot, context.Object, CancellationToken.None))
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
    public async Task GIVEN_TypeSymbolHasNoSourceLocation_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var type = CreateNamedTypeSymbol([]);
        var target = new MoveTypeToFileTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Type, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = type.Object,
            });

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TypeSourceLocationCannotBeProjected_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var location = RoslynTestFactory.CreateSourceLocation();
        var type = CreateNamedTypeSymbol([location]);
        var target = new MoveTypeToFileTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Type, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = type.Object,
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
    public async Task GIVEN_TypeSourceLocationCanBeProjected_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeAction()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var requestResolver = new Mock<IToolRequestResolver>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(requestResolver, workspaceResolver);
        var request = CreateRequest();
        var location = RoslynTestFactory.CreateSourceLocation();
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation(location, "Code.cs");
        var type = CreateNamedTypeSymbol([location]);
        var target = new MoveTypeToFileTool();

        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(request.Type, request.ExpectedSnapshot, context.Object, CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = type.Object,
            });
        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns(resolvedLocation);
        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.MoveType.MoveTypeCodeRefactoringProvider"
                    && replayRequest.TitleStartsWith == "Move type to "
                    && replayRequest.Location != null),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.MoveType.MoveTypeCodeRefactoringProvider"
                && replayRequest.TitleStartsWith == "Move type to "
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

    private static MoveTypeToFileRequest CreateRequest(bool preserveNamespace = true)
    {
        return new MoveTypeToFileRequest
        {
            Type = new SymbolSelector
            {
                DocumentationCommentId = "DocumentationCommentId",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            PreserveNamespace = preserveNamespace,
        };
    }

    private static Mock<INamedTypeSymbol> CreateNamedTypeSymbol(IReadOnlyList<Location> locations)
    {
        var symbol = new Mock<INamedTypeSymbol>();

        symbol
            .Setup(item => item.Locations)
            .Returns(System.Collections.Immutable.ImmutableArray.CreateRange(locations));

        return symbol;
    }
}
