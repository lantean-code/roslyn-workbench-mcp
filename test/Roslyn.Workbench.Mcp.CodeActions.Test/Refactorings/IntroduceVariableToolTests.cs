namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class IntroduceVariableToolTests
{
    [Theory]
    [InlineData((int)IntroduceVariableKind.Local, "Introduce local for ", "all occurrences")]
    [InlineData((int)IntroduceVariableKind.LocalAllOccurrences, "Introduce local for all occurrences of ", null)]
    [InlineData((int)IntroduceVariableKind.LocalConstant, "Introduce local constant for ", "all occurrences")]
    [InlineData((int)IntroduceVariableKind.LocalConstantAllOccurrences, "Introduce local constant for all occurrences of ", null)]
    [InlineData((int)IntroduceVariableKind.Constant, "Introduce constant for ", "all occurrences")]
    [InlineData((int)IntroduceVariableKind.ConstantAllOccurrences, "Introduce constant for all occurrences of ", null)]
    [InlineData((int)IntroduceVariableKind.Field, "Introduce field for ", "all occurrences")]
    [InlineData((int)IntroduceVariableKind.FieldAllOccurrences, "Introduce field for all occurrences of ", null)]
    [InlineData((int)IntroduceVariableKind.QueryVariable, "Introduce query variable for ", "all occurrences")]
    [InlineData((int)IntroduceVariableKind.QueryVariableAllOccurrences, "Introduce query variable for all occurrences of ", null)]
    public async Task GIVEN_IntroduceVariableKind_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayActionWithExpectedTitleFilters(
        int kindValue,
        string titleStartsWith,
        string? titleDoesNotContain)
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var kind = (IntroduceVariableKind)kindValue;
        var request = new IntroduceVariableRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Kind = kind,
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new IntroduceVariableTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.TitleStartsWith == titleStartsWith
                    && stageRequest.TitleDoesNotContain == titleDoesNotContain),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.TitleStartsWith == titleStartsWith
                && stageRequest.TitleDoesNotContain == titleDoesNotContain),
            context.Object, CancellationToken.None), Times.Once);
    }
}
