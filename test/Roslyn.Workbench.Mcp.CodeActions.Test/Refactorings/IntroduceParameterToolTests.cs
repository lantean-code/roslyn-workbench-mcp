namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class IntroduceParameterToolTests
{
    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new IntroduceParameterTool(replayService.Object);

        var result = await target.ExecuteAsync(new IntroduceParameterRequest(), context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        replayService.Verify(item => item.StageReplayCodeActionAsync(
            It.IsAny<ReplayCodeActionRequest>(),
            context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(IntroduceParameterStrategy.UpdateCallSitesDirectly, false, "and update call sites directly", 0, 0)]
    [InlineData(IntroduceParameterStrategy.UpdateCallSitesDirectly, true, "and update call sites directly", 1, 0)]
    [InlineData(IntroduceParameterStrategy.IntoExtractedMethod, false, "into extracted method to invoke at call sites", 0, 1)]
    [InlineData(IntroduceParameterStrategy.IntoExtractedMethod, true, "into extracted method to invoke at call sites", 1, 1)]
    [InlineData(IntroduceParameterStrategy.IntoNewOverload, false, "into new overload", 0, 2)]
    [InlineData(IntroduceParameterStrategy.IntoNewOverload, true, "into new overload", 1, 2)]
    public async Task GIVEN_StrategyAndOccurrenceSelection_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayActionWithExpectedPath(
        IntroduceParameterStrategy strategy,
        bool allOccurrences,
        string title,
        int firstPathSegment,
        int secondPathSegment)
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new IntroduceParameterRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Strategy = strategy,
            AllOccurrences = allOccurrences,
        };
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new IntroduceParameterTool(replayService.Object);

        replayService
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider"
                    && stageRequest.Title == title
                    && stageRequest.EquivalenceKey == title
                    && stageRequest.ActionPath != null
                    && stageRequest.ActionPath.Count == 2
                    && stageRequest.ActionPath[0] == firstPathSegment
                    && stageRequest.ActionPath[1] == secondPathSegment),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayService.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider"
                && stageRequest.Title == title
                && stageRequest.EquivalenceKey == title
                && stageRequest.ActionPath != null
                && stageRequest.ActionPath.Count == 2
                && stageRequest.ActionPath[0] == firstPathSegment
                && stageRequest.ActionPath[1] == secondPathSegment),
            context.Object, CancellationToken.None), Times.Once);
    }
}
