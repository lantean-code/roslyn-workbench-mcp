namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertExpressionBodyToolTests
{
    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsNonUnavailableResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnPrimaryResult()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertExpressionBodyTool(replayService.Object);

        replayService
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsDifferentRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnPrimaryRejection()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
        {
            Code = "InvalidRequest",
            Message = "InvalidRequest",
        });
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertExpressionBodyTool(replayService.Object);

        replayService
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsRejectionWithoutError_WHEN_CallingExecuteAsync_THEN_ShouldReturnPrimaryRejection()
    {
        var expected = new CodeActionExecutionResult<WorkspaceMutationCandidate>
        {
            Outcome = CodeActionExecutionOutcome.Rejected,
        };
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertExpressionBodyTool(replayService.Object);

        replayService
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsUnavailableRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnLambdaProviderResult()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new ConvertExpressionBodyTool(replayService.Object);

        replayService
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "CodeActionUnavailable",
                Message = "CodeActionUnavailable",
            }));
        replayService
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        replayService.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
    }
}
