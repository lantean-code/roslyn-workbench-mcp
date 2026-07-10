namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

public sealed class StageCodeFixToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        StageCodeFixTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<StageCodeFixRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "stage-code-fix"
                && metadata.Title == "Stage Code Fix"
                && metadata.Description == "Revalidates and stages one selected code fix into the active transaction."
                && metadata.Behavior.Destructive),
            It.IsAny<CodeActionMutationToolHandler<StageCodeFixRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
        var request = new StageCodeFixRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();
        var target = new StageCodeFixTool();

        context
            .Setup(item => item.StageCodeFixAsync(request, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageCodeFixAsync(request, CancellationToken.None), Times.Once);
    }
}
