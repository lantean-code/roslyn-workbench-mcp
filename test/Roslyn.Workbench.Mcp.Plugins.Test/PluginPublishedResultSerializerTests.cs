namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginPublishedResultSerializerTests
{
    [Fact]
    public void GIVEN_QueryScalarPayload_WHEN_Serializing_THEN_ShouldThrowInvalidOperationException()
    {
        var result = PluginExecutionResult<string>.Success("Value");

        var action = () => PluginPublishedResultSerializer.SerializeQuery(result);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Published response type 'System.String' must serialize as a JSON object.");
    }

    [Fact]
    public void GIVEN_NonErrorFailureResult_WHEN_SerializingFailure_THEN_ShouldThrowInvalidOperationException()
    {
        var result = new ToolExecutionFailureResult
        {
            Outcome = ToolOutcome.Succeeded,
            Error = new ToolError
            {
                Code = "Code",
                Message = "Message",
            },
        };

        var action = () => PluginPublishedResultSerializer.SerializeFailure(result);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Failure serialization requires an error outcome, but 'Succeeded' was supplied.");
    }
}
