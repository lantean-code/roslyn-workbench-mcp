namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ExtractMethodToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        ExtractMethodTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<ExtractMethodRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "extract-method"
                && metadata.Title == "Extract Method"
                && metadata.Description == "Extracts a selected statement or expression block through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<ExtractMethodRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var target = new ExtractMethodTool();

        var result = await target.ExecuteAsync(new ExtractMethodRequest(), context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.IsAny<ReplayCodeActionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocalFunctionTargetKind_WHEN_CallingExecuteAsync_THEN_ShouldStageLocalFunctionReplayAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
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
        var target = new ExtractMethodTool();

        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                    && stageRequest.Title == "Extract local function"
                    && stageRequest.EquivalenceKey == "Extract_local_function"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                && stageRequest.Title == "Extract local function"
                && stageRequest.EquivalenceKey == "Extract_local_function"),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MethodTargetKind_WHEN_CallingExecuteAsync_THEN_ShouldStageMethodReplayAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
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
        var target = new ExtractMethodTool();

        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                    && stageRequest.Title == "Extract method"
                    && stageRequest.EquivalenceKey == "Extract_method"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider"
                && stageRequest.Title == "Extract method"
                && stageRequest.EquivalenceKey == "Extract_method"),
            CancellationToken.None), Times.Once);
    }
}
