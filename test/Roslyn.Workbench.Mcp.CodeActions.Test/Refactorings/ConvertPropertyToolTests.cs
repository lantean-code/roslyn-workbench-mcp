namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertPropertyToolTests
{
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

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var locationFixStager = new Mock<ILocationCodeFixStager>();
        var target = new ConvertPropertyTool(selectionStager.Object, locationFixStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
                "Convert to full property",
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
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

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var locationFixStager = new Mock<ILocationCodeFixStager>();
        var target = new ConvertPropertyTool(selectionStager.Object, locationFixStager.Object);

        locationFixStager
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
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            It.Is<LocationCodeFixRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider"
                && stageRequest.AnalyzerTypeName == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer"
                && stageRequest.SyntheticDiagnosticId == "IDE0032"
                && stageRequest.DiagnosticIds.Count == 1
                && stageRequest.DiagnosticIds[0] == "IDE0032"
                && stageRequest.Title == "Use auto property"),
            context.Object,
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

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var locationFixStager = new Mock<ILocationCodeFixStager>();
        var target = new ConvertPropertyTool(selectionStager.Object, locationFixStager.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        selectionStager.Verify(item => item.StageSelectionAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<CancellationToken>(),
            context.Object,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<int>?>()), Times.Never);

        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            It.IsAny<LocationCodeFixRequest>(),
            context.Object,
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
