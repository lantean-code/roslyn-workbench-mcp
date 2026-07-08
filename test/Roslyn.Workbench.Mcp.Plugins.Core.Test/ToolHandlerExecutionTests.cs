namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class ToolHandlerExecutionTests
{
    [Fact]
    public async Task GIVEN_QueryCancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new TestQueryTool();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.ExecuteAsync(new TestWorkspaceBoundRequest(), Mock.Of<IQueryContext>(), cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_NullQueryContext_WHEN_CallingExecuteAsync_THEN_ShouldThrowArgumentNullException()
    {
        var target = new TestQueryTool();

        var action = async () => await target.ExecuteAsync(new TestWorkspaceBoundRequest(), null!, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_MutationCancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new TestMutationTool();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.ExecuteAsync(new TestWorkspaceBoundRequest(), Mock.Of<IMutationContext>(), cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_NullMutationContext_WHEN_CallingExecuteAsync_THEN_ShouldThrowArgumentNullException()
    {
        var target = new TestMutationTool();

        var action = async () => await target.ExecuteAsync(new TestWorkspaceBoundRequest(), null!, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed record TestWorkspaceBoundRequest : WorkspaceBoundRequest;

    private sealed class TestQueryTool : QueryToolHandler<TestWorkspaceBoundRequest, string>
    {
        protected override ValueTask<PluginExecutionResult<string>> ExecuteCoreAsync(TestWorkspaceBoundRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<string>.Success("Value"));
        }
    }

    private sealed class TestMutationTool : MutationToolHandler<TestWorkspaceBoundRequest>
    {
        protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(TestWorkspaceBoundRequest request, IMutationContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<MutationProposal>.Success(new MutationProposal()));
        }
    }
}
