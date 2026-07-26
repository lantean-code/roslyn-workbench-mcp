namespace Roslyn.Workbench.Mcp.Plugins.Test.Execution;

public sealed class ToolExecutionContextLeaseTests
{
    [Fact]
    public async Task GIVEN_RejectedLease_WHEN_Disposed_THEN_ShouldNotThrow()
    {
        var target = ToolExecutionContextLease.Rejected<IQueryContext>(new ToolExecutionFailureResult
        {
            Outcome = PluginExecutionOutcome.Rejected,
            Error = new PluginExecutionError
            {
                Code = "Code",
                Message = "Message",
            },
        });

        var action = async () => await target.DisposeAsync();

        await action.Should().NotThrowAsync();
    }
}
