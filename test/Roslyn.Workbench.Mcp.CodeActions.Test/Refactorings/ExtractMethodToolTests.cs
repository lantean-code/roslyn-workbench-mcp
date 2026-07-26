namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ExtractMethodToolTests
{
    [Fact]
    public async Task GIVEN_LocalFunctionTargetKind_WHEN_CallingExecuteAsync_THEN_ShouldStageLocalFunctionReplayAction()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ExtractMethodRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            TargetKind = ExtractMethodTargetKind.LocalFunction,
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ExtractMethodTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                    && stageRequest.Title == "Extract local function"
                    && stageRequest.EquivalenceKey == "Extract_local_function"),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                && stageRequest.Title == "Extract local function"
                && stageRequest.EquivalenceKey == "Extract_local_function"),
            context.Object, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MethodTargetKind_WHEN_CallingExecuteAsync_THEN_ShouldStageMethodReplayAction()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ExtractMethodRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            TargetKind = ExtractMethodTargetKind.Method,
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ExtractMethodTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                    && stageRequest.Title == "Extract method"
                    && stageRequest.EquivalenceKey == "Extract_method"),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                && stageRequest.Title == "Extract method"
                && stageRequest.EquivalenceKey == "Extract_method"),
            context.Object, CancellationToken.None), Times.Once);
    }
}
