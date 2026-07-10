namespace Roslyn.Workbench.Mcp.Test.Protocol;

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

    [Fact]
    public void GIVEN_NonErrorCodeActionFailure_WHEN_SerializingFailure_THEN_ShouldThrowInvalidOperationException()
    {
        var result = new CodeActionExecutionFailure
        {
            Outcome = CodeActionExecutionOutcome.Succeeded,
        };

        var action = () => McpPublishedResultSerializer.SerializeCodeActionFailure(result);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Failure serialization requires an error outcome, but 'Succeeded' was supplied.");
    }

    [Fact]
    public void GIVEN_NullPluginFailure_WHEN_Serializing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => McpPublishedResultSerializer.SerializePluginFailure(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullPluginQueryResult_WHEN_Serializing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => McpPublishedResultSerializer.SerializePluginQuery<string>(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullPluginMutationResult_WHEN_Serializing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => McpPublishedResultSerializer.SerializePluginMutation(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullCodeActionFailure_WHEN_Serializing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => McpPublishedResultSerializer.SerializeCodeActionFailure(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullCodeActionQueryResult_WHEN_Serializing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => McpPublishedResultSerializer.SerializeCodeActionQuery<string>(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_NullCodeActionMutationResult_WHEN_Serializing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => McpPublishedResultSerializer.SerializeCodeActionMutation(null!);

        action.Should().Throw<ArgumentNullException>();
    }
}
