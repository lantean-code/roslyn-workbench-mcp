namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

public sealed class CodeActionDelegationToolTests
{
    [Fact]
    public async Task GIVEN_StageCodeActionRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var request = new StageCodeActionRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();

        context
            .Setup(item => item.StageCodeActionAsync(request, CancellationToken.None))
            .ReturnsAsync(expected);

        var target = new StageCodeActionTool();

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_StageCodeFixRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var request = new StageCodeFixRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();

        context
            .Setup(item => item.StageCodeFixAsync(request, CancellationToken.None))
            .ReturnsAsync(expected);

        var target = new StageCodeFixTool();

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_StageFixAllRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationContextResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var request = new StageFixAllRequest
        {
            ActionId = "ActionId",
        };
        var context = new Mock<ICodeActionMutationContext>();

        context
            .Setup(item => item.StageFixAllAsync(request, CancellationToken.None))
            .ReturnsAsync(expected);

        var target = new StageFixAllTool();

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
