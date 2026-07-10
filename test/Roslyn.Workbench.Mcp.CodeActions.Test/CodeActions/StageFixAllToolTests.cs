namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

public sealed class StageFixAllToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        StageFixAllTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<StageFixAllRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "stage-fix-all"
                && metadata.Title == "Stage Fix All"
                && metadata.Description == "Revalidates one selected code fix and stages its fix-all variant into the active transaction."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<StageFixAllRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MutationContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var request = new StageFixAllRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();
        var target = new StageFixAllTool();

        context
            .Setup(item => item.StageFixAllAsync(request, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageFixAllAsync(request, CancellationToken.None), Times.Once);
    }
}
