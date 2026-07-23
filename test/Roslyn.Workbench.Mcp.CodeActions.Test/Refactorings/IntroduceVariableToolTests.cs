namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class IntroduceVariableToolTests
{
    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new IntroduceVariableTool(selectionStager.Object);

        var result = await target.ExecuteAsync(new IntroduceVariableRequest(), context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.IsAny<ReplayCodeActionRequest>(),
            context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(IntroduceVariableKind.Local, "Introduce local for ", "all occurrences")]
    [InlineData(IntroduceVariableKind.LocalAllOccurrences, "Introduce local for all occurrences of ", null)]
    [InlineData(IntroduceVariableKind.LocalConstant, "Introduce local constant for ", "all occurrences")]
    [InlineData(IntroduceVariableKind.LocalConstantAllOccurrences, "Introduce local constant for all occurrences of ", null)]
    [InlineData(IntroduceVariableKind.Constant, "Introduce constant for ", "all occurrences")]
    [InlineData(IntroduceVariableKind.ConstantAllOccurrences, "Introduce constant for all occurrences of ", null)]
    [InlineData(IntroduceVariableKind.Field, "Introduce field for ", "all occurrences")]
    [InlineData(IntroduceVariableKind.FieldAllOccurrences, "Introduce field for all occurrences of ", null)]
    [InlineData(IntroduceVariableKind.QueryVariable, "Introduce query variable for ", "all occurrences")]
    [InlineData(IntroduceVariableKind.QueryVariableAllOccurrences, "Introduce query variable for all occurrences of ", null)]
    public async Task GIVEN_IntroduceVariableKind_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayActionWithExpectedTitleFilters(
        IntroduceVariableKind kind,
        string titleStartsWith,
        string? titleDoesNotContain)
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
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
