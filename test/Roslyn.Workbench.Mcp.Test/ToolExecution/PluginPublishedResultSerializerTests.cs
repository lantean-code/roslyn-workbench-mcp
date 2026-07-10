namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class McpPublishedResultSerializerTests
{
    [Fact]
    public void GIVEN_QueryScalarPayload_WHEN_Serializing_THEN_ShouldThrowInvalidOperationException()
    {
        var result = PluginExecutionResult<string>.Success("Value");

        var action = () => McpPublishedResultSerializer.SerializePluginQuery(result);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Published response type 'System.String' must serialize as a JSON object.");
    }

    [Fact]
    public void GIVEN_NonErrorFailureResult_WHEN_SerializingFailure_THEN_ShouldThrowInvalidOperationException()
    {
        var result = new ToolExecutionFailureResult
        {
            Outcome = PluginExecutionOutcome.Succeeded,
            Error = new PluginExecutionError
            {
                Code = "Code",
                Message = "Message",
            },
        };

        var action = () => McpPublishedResultSerializer.SerializePluginFailure(result);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Failure serialization requires an error outcome, but 'Succeeded' was supplied.");
    }
}
