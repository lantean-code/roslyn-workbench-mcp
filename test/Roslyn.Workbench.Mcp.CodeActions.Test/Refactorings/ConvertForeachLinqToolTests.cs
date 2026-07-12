namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertForeachLinqToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        ConvertForeachLinqTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<ConvertForeachLinqRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "convert-foreach-linq"
                && metadata.Title == "Convert Foreach LINQ"
                && metadata.Description == "Stages one supported Roslyn foreach or LINQ conversion through refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<ConvertForeachLinqRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var target = new ConvertForeachLinqTool();

        var result = await target.ExecuteAsync(new ConvertForeachLinqRequest(), context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.IsAny<ReplayCodeActionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
        var target = new ConvertForeachLinqTool();

        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                    && stageRequest.Title == "Convert to LINQ call form"
                    && stageRequest.EquivalenceKey == "Convert_to_linq_call_form"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                && stageRequest.Title == "Convert to LINQ call form"
                && stageRequest.EquivalenceKey == "Convert_to_linq_call_form"),
            CancellationToken.None), Times.Once);
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
        var target = new ConvertForeachLinqTool();

        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider"
                    && stageRequest.Title == "Convert to foreach"
                    && stageRequest.EquivalenceKey == "Convert_to_foreach"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider"
                && stageRequest.Title == "Convert to foreach"
                && stageRequest.EquivalenceKey == "Convert_to_foreach"),
            CancellationToken.None), Times.Once);
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
        var target = new ConvertForeachLinqTool();

        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                    && stageRequest.Title == "Convert to LINQ"
                    && stageRequest.EquivalenceKey == "Convert_to_linq"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider"
                && stageRequest.Title == "Convert to LINQ"
                && stageRequest.EquivalenceKey == "Convert_to_linq"),
            CancellationToken.None), Times.Once);
    }
}
