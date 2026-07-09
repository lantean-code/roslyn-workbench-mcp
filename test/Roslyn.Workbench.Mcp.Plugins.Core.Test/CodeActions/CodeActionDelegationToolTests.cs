namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.CodeActions;

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
        var context = new MutationContextBuilder()
            .WithStageCodeActionAsync((actualRequest, cancellationToken) =>
            {
                actualRequest.Should().BeEquivalentTo(request);
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new StageCodeActionTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

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
        var context = new MutationContextBuilder()
            .WithStageCodeFixAsync((actualRequest, cancellationToken) =>
            {
                actualRequest.Should().BeEquivalentTo(request);
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new StageCodeFixTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

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
        var context = new MutationContextBuilder()
            .WithStageFixAllAsync((actualRequest, cancellationToken) =>
            {
                actualRequest.Should().BeEquivalentTo(request);
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new StageFixAllTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
