namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertPropertyToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        ConvertPropertyTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<ConvertPropertyRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "convert-property"
                && metadata.Title == "Convert Property"
                && metadata.Description == "Converts one selected property between supported auto-property and full-property forms through Roslyn composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<ConvertPropertyRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ToFullDirection_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithFullPropertyProvider()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertPropertyRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Direction = ConvertPropertyDirection.ToFull,
        };
        var target = new ConvertPropertyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
                "Convert to full property",
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
            "Convert to full property",
            null,
            null,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ToAutoWhenSafeDirection_WHEN_CallingExecuteAsync_THEN_ShouldStageLocationCodeFixWithAutoPropertyProvider()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertPropertyRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Direction = ConvertPropertyDirection.ToAutoWhenSafe,
        };
        var target = new ConvertPropertyTool();

        context
            .Setup(item => item.StageLocationCodeFixAsync(
                It.Is<LocationCodeFixRequest>(stageRequest =>
                    stageRequest.Location == request.Selection
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider"
                    && stageRequest.AnalyzerTypeName == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer"
                    && stageRequest.SyntheticDiagnosticId == "IDE0032"
                    && stageRequest.DiagnosticIds.Count == 1
                    && stageRequest.DiagnosticIds[0] == "IDE0032"
                    && stageRequest.Title == "Use auto property"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageLocationCodeFixAsync(
            It.Is<LocationCodeFixRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider"
                && stageRequest.AnalyzerTypeName == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer"
                && stageRequest.SyntheticDiagnosticId == "IDE0032"
                && stageRequest.DiagnosticIds.Count == 1
                && stageRequest.DiagnosticIds[0] == "IDE0032"
                && stageRequest.Title == "Use auto property"),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_UnsupportedDirection_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertPropertyRequest
        {
            Selection = new LocationSelector(),
            Direction = (ConvertPropertyDirection)999,
        };
        var target = new ConvertPropertyTool();

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        context.Verify(item => item.StageReplaySelectionAsync(
            It.IsAny<LocationSelector?>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<int>?>()), Times.Never);
        context.Verify(item => item.StageLocationCodeFixAsync(
            It.IsAny<LocationCodeFixRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
