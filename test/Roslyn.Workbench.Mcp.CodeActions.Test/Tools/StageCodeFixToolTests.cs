namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class StageCodeFixToolTests
{
    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = new StageCodeFixRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            ActionId = "ActionId",
        };

        var context = new Mock<ICodeActionMutationContext>();
        var tokenStager = new Mock<ICodeActionTokenStager>();
        var target = new StageCodeFixTool(tokenStager.Object);

        tokenStager
            .Setup(item => item.StageCodeFixAsync(request, context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        tokenStager.Verify(item => item.StageCodeFixAsync(request, context.Object, CancellationToken.None), Times.Once);
    }
}
