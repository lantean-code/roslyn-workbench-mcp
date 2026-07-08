namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.CodeActions;

public sealed class CodeActionDelegationToolTests
{
    [Fact]
    public async Task GIVEN_DescribeCodeActionRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnQueryContextResult()
    {
        var expected = PluginExecutionResult<DescribeCodeActionData>.Success(new DescribeCodeActionData());
        var request = new DescribeCodeActionRequest
        {
            ActionId = "ActionId",
        };
        var context = new QueryContextBuilder()
            .WithDescribeCodeActionAsync((actualRequest, cancellationToken) =>
            {
                actualRequest.Should().BeEquivalentTo(request);
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new DescribeCodeActionTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ListCodeActionsRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnQueryContextResult()
    {
        var expected = PluginExecutionResult<CodeActionListData>.Success(new CodeActionListData());
        var request = new ListCodeActionsRequest
        {
            Location = new LocationSelector(),
        };
        var context = new QueryContextBuilder()
            .WithListCodeActionsAsync((actualRequest, cancellationToken) =>
            {
                actualRequest.Should().BeEquivalentTo(request);
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new ListCodeActionsTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

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
