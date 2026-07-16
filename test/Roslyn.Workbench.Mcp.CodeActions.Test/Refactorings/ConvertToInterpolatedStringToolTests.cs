namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertToInterpolatedStringToolTests
{
    [Fact]
    public async Task GIVEN_SnapshotValidationReturnsRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnSnapshotRejection()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Conflict(new CodeActionExecutionError
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
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertToInterpolatedStringTool(replayService.Object);

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
        replayService.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
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
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertToInterpolatedStringTool(replayService.Object);

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
        replayService.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
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
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertToInterpolatedStringTool(replayService.Object);

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
        replayService.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationResolutionIsResolved_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
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
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertToInterpolatedStringTool(replayService.Object);

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.Matched());
        workspaceResolver
            .Setup(item => item.ResolveLocationAsync(request.Selection, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        replayService
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.Location == request.Selection
                    && replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.Title == "Convert to interpolated string"
                    && replayRequest.EquivalenceKey == "Convert_to_interpolated_string"),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayService.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.Location == request.Selection
                && replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.Title == "Convert to interpolated string"
                && replayRequest.EquivalenceKey == "Convert_to_interpolated_string"),
            context.Object, CancellationToken.None), Times.Once);
    }
}
