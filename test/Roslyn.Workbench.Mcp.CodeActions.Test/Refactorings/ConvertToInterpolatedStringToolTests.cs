namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertToInterpolatedStringToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        ConvertToInterpolatedStringTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<ConvertToInterpolatedStringRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "convert-to-interpolated-string"
                && metadata.Title == "Convert To Interpolated String"
                && metadata.Description == "Converts a supported string expression to an interpolated string through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<ConvertToInterpolatedStringRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SnapshotValidationReturnsRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnSnapshotRejection()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Conflict(new CodeActionExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "The request snapshot does not match the current workspace snapshot.",
        }, RequiredAction.ResolveTargetAgain);
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertToInterpolatedStringTool();

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.WorkspaceEpochMismatch());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        workspaceResolver.Verify(
            item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_RequestWithoutSelection_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = null,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertToInterpolatedStringTool();

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.Matched());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("InvalidRequest");
        workspaceResolver.Verify(
            item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationResolutionIsNotResolved_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertToInterpolatedStringTool();

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.Matched());
        workspaceResolver
            .Setup(item => item.ResolveLocationAsync(request.Selection, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("LocationNotFound");
        context.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationResolutionIsResolved_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        var location = Location.None;
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertToInterpolatedStringTool();

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.Matched());
        workspaceResolver
            .Setup(item => item.ResolveLocationAsync(request.Selection, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.Location == request.Selection
                    && replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.Title == "Convert to interpolated string"
                    && replayRequest.EquivalenceKey == "Convert_to_interpolated_string"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.Location == request.Selection
                && replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.Title == "Convert to interpolated string"
                && replayRequest.EquivalenceKey == "Convert_to_interpolated_string"),
            CancellationToken.None), Times.Once);
    }
}
