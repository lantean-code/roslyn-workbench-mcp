namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class StageCodeActionToolTests
{
    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = new StageCodeActionRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            ActionId = Guid.Empty,
        };

        var context = new Mock<ICodeActionMutationContext>();
        var referenceStager = new Mock<ICodeActionReferenceStager>();
        var target = new StageCodeActionTool(referenceStager.Object);

        referenceStager
            .Setup(item => item.StageCodeActionAsync(request, context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        referenceStager.Verify(item => item.StageCodeActionAsync(request, context.Object, CancellationToken.None), Times.Once);
    }
}
