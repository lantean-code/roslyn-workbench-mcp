using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertToInterpolatedStringToolTests
{
    [Fact]
    public async Task GIVEN_SnapshotValidationReturnsRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnSnapshotRejection()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Conflict(new CodeActionExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "The request snapshot does not match the current workspace snapshot.",
        }, RequiredAction.ResolveTargetAgain);
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.WorkspaceEpochMismatch());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        workspaceResolver.Verify(
            item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()),
            Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_RequestWithoutSelection_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = null,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.Matched());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("InvalidRequest");
        workspaceResolver.Verify(
            item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()),
            Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationResolutionIsNotResolved_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.Matched());
        workspaceResolver
            .Setup(item => item.ResolveLocationAsync(request.Selection, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("LocationNotFound");
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationResolutionIsResolved_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeAction()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = new Mock<ICodeActionMutationContext>();
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var location = await CreateLocationAsync(roslyn.Document);
        var request = new ConvertToInterpolatedStringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        workspaceResolver
            .Setup(item => item.ValidateSnapshot(request.ExpectedSnapshot))
            .Returns(SnapshotMatchResult.Matched());
        workspaceResolver
            .Setup(item => item.ResolveLocationAsync(request.Selection, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.Location == request.Selection
                    && replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.Title == "Convert to interpolated string"
                    && replayRequest.EquivalenceKey == "Convert_to_interpolated_string"),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.Location == request.Selection
                && replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.Title == "Convert to interpolated string"
                && replayRequest.EquivalenceKey == "Convert_to_interpolated_string"),
            context.Object, CancellationToken.None), Times.Once);
    }

    private static ConvertToInterpolatedStringTool CreateTarget(ICodeActionSelectionStager selectionStager)
    {
        var requestResolver = new CodeActionToolRequestResolver(new CodeActionScopeResolver());

        return new ConvertToInterpolatedStringTool(selectionStager, requestResolver);
    }

    private static async Task<Location> CreateLocationAsync(Document document)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(CancellationToken.None);
        return Location.Create(syntaxTree!, new TextSpan(0, 1));
    }
}
