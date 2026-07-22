namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertForeachLinqToolTests
{
    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ConvertForeachLinqTool(selectionStager.Object);

        var result = await target.ExecuteAsync(new ConvertForeachLinqRequest(), context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.IsAny<ReplayCodeActionRequest>(),
            context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ForeachToCallFormKind_WHEN_CallingExecuteAsync_THEN_ShouldStageCallFormReplayAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertForeachLinqRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            ConversionKind = ConvertForeachLinqKind.ForeachToCallForm,
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ConvertForeachLinqTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                    && stageRequest.Title == "Convert to LINQ call form"
                    && stageRequest.EquivalenceKey == "Convert_to_linq_call_form"),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                && stageRequest.Title == "Convert to LINQ call form"
                && stageRequest.EquivalenceKey == "Convert_to_linq_call_form"),
            context.Object, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LinqToForeachKind_WHEN_CallingExecuteAsync_THEN_ShouldStageForeachReplayAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertForeachLinqRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            ConversionKind = ConvertForeachLinqKind.LinqToForeach,
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ConvertForeachLinqTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider"
                    && stageRequest.Title == "Convert to foreach"
                    && stageRequest.EquivalenceKey == "Convert_to_foreach"),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider"
                && stageRequest.Title == "Convert to foreach"
                && stageRequest.EquivalenceKey == "Convert_to_foreach"),
            context.Object, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ForeachToQueryKind_WHEN_CallingExecuteAsync_THEN_ShouldStageQueryReplayAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertForeachLinqRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            ConversionKind = ConvertForeachLinqKind.ForeachToQuery,
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ConvertForeachLinqTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                    && stageRequest.Title == "Convert to LINQ"
                    && stageRequest.EquivalenceKey == "Convert_to_linq"),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                && stageRequest.Title == "Convert to LINQ"
                && stageRequest.EquivalenceKey == "Convert_to_linq"),
            context.Object, CancellationToken.None), Times.Once);
    }
}
