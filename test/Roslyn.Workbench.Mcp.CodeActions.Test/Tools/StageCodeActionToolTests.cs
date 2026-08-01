namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class StageCodeActionToolTests
{
    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = new StageCodeActionRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition { WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            ActionId = Guid.Empty,
        };

        var context = new Mock<ICodeActionMutationContext>();
        var stager = new Mock<ICodeActionStager>();
        var target = new StageCodeActionTool(stager.Object);

        stager
            .Setup(item => item.StageAsync(request, context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        stager.Verify(item => item.StageAsync(request, context.Object, CancellationToken.None), Times.Once);
    }
}
