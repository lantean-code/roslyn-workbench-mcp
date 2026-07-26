namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class StageCodeFixToolTests
{
    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = new StageCodeFixRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            ActionId = Guid.Empty,
        };

        var context = new Mock<ICodeActionMutationContext>();
        var referenceStager = new Mock<ICodeActionReferenceStager>();
        var target = new StageCodeFixTool(referenceStager.Object);

        referenceStager
            .Setup(item => item.StageCodeFixAsync(request, context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        referenceStager.Verify(item => item.StageCodeFixAsync(request, context.Object, CancellationToken.None), Times.Once);
    }
}
