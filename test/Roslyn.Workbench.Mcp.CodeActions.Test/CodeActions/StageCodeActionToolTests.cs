namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

public sealed class StageCodeActionToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        StageCodeActionTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<StageCodeActionRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "stage-code-action"
                && metadata.Title == "Stage Code Action"
                && metadata.Description == "Revalidates and stages one selected refactoring action into the active transaction."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<StageCodeActionRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
        var request = new StageCodeActionRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();
        var target = new StageCodeActionTool();

        context
            .Setup(item => item.StageCodeActionAsync(request, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageCodeActionAsync(request, CancellationToken.None), Times.Once);
    }
}
