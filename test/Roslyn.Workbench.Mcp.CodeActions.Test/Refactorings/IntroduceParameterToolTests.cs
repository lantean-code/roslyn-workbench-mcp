namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class IntroduceParameterToolTests
{
    [Theory]
    [InlineData((int)IntroduceParameterStrategy.UpdateCallSitesDirectly, false, "and update call sites directly", 0, 0)]
    [InlineData((int)IntroduceParameterStrategy.UpdateCallSitesDirectly, true, "and update call sites directly", 1, 0)]
    [InlineData((int)IntroduceParameterStrategy.IntoExtractedMethod, false, "into extracted method to invoke at call sites", 0, 1)]
    [InlineData((int)IntroduceParameterStrategy.IntoExtractedMethod, true, "into extracted method to invoke at call sites", 1, 1)]
    [InlineData((int)IntroduceParameterStrategy.IntoNewOverload, false, "into new overload", 0, 2)]
    [InlineData((int)IntroduceParameterStrategy.IntoNewOverload, true, "into new overload", 1, 2)]
    public async Task GIVEN_StrategyAndOccurrenceSelection_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayActionWithExpectedPath(
        int strategyValue,
        bool allOccurrences,
        string title,
        int firstPathSegment,
        int secondPathSegment)
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var strategy = (IntroduceParameterStrategy)strategyValue;
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

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new IntroduceParameterTool(selectionStager.Object);

        selectionStager
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
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
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
