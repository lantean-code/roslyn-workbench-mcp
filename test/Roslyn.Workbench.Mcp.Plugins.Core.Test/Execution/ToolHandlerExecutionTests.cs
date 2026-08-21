namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Execution;

public sealed class ToolHandlerExecutionTests
{
    [Fact]
    public async Task GIVEN_QueryCancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new TestQueryTool();
        var context = new Mock<IQueryContext>();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var request = new TestWorkspaceBoundRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        };
        var action = async () => await target.ExecuteAsync(request, context.Object, cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_MutationCancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new TestMutationTool();
        var context = new Mock<IMutationContext>();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var request = new TestWorkspaceBoundRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        };
        var action = async () => await target.ExecuteAsync(request, context.Object, cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed record TestWorkspaceBoundRequest : WorkspaceMutationRequest;

    private sealed record TestQueryResponse : IQueryResponse
    {
    }

    private sealed class TestQueryTool : QueryToolHandler<TestWorkspaceBoundRequest, TestQueryResponse>
    {
        protected override ValueTask<PluginExecutionResult<TestQueryResponse>> ExecuteCoreAsync(TestWorkspaceBoundRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new TestQueryResponse();
            return ValueTask.FromResult(PluginExecutionResult.Success(response));
        }
    }

    private sealed class TestMutationTool : MutationToolHandler<TestWorkspaceBoundRequest>
    {
        protected override ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteCoreAsync(TestWorkspaceBoundRequest request, IMutationContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.Success(MutationCandidateTestData.CreatePluginCandidate()));
        }
    }
}
