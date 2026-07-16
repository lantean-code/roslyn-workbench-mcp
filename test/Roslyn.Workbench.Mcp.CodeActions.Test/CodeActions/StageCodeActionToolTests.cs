namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class StageCodeActionToolTests
{
    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = new StageCodeActionRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();
        var replayService = new Mock<ICodeActionReplayService>();
        var target = new StageCodeActionTool(replayService.Object);

        replayService
            .Setup(item => item.StageCodeActionAsync(request, context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayService.Verify(item => item.StageCodeActionAsync(request, context.Object, CancellationToken.None), Times.Once);
    }
}
