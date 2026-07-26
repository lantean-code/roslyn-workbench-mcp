namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class StageFixAllToolTests
{
    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = new StageFixAllRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            ActionId = Guid.Empty,
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Solution,
            },
        };

        var context = new Mock<ICodeActionMutationContext>();
        var fixAllStager = new Mock<ICodeActionFixAllStager>();
        var target = new StageFixAllTool(fixAllStager.Object);

        fixAllStager
            .Setup(item => item.StageFixAllAsync(request, context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        fixAllStager.Verify(item => item.StageFixAllAsync(request, context.Object, CancellationToken.None), Times.Once);
    }
}
