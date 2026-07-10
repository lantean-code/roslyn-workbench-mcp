namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class ToolExecutionContextLeaseTests
{
    [Fact]
    public async Task GIVEN_RejectedLease_WHEN_Disposed_THEN_ShouldNotThrow()
    {
        var target = ToolExecutionContextLease<IQueryContext>.Rejected(new ToolExecutionFailureResult
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
