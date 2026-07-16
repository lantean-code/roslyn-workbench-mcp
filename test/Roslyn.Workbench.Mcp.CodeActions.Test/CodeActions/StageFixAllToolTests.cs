namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class StageFixAllToolTests
{
    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = new StageFixAllRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();
        var fixAllService = new Mock<ICodeActionFixAllService>();
        var target = new StageFixAllTool(fixAllService.Object);

        fixAllService
            .Setup(item => item.StageFixAllAsync(request, context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        fixAllService.Verify(item => item.StageFixAllAsync(request, context.Object, CancellationToken.None), Times.Once);
    }
}
