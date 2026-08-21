namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpPublishedResultSerializerTests
{
    [Fact]
    public void GIVEN_QueryScalarPayload_WHEN_Serializing_THEN_ShouldThrowInvalidOperationException()
    {
        var result = PluginExecutionResult.Success("Value");

        var action = () => McpPublishedResultSerializer.SerializePluginQuery(result, CreateSnapshot());

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
    public void GIVEN_PluginFailureDetails_WHEN_SerializingFailure_THEN_ShouldPublishDetails()
    {
        var result = PluginExecutionResult.Rejected<TestData>(
            new PluginExecutionError
            {
                Code = "Code",
                Message = "Message",
            },
            diagnostics:
            [
                new DiagnosticInfo
                {
                    Id = "Id",
                    Message = "Message",
                },
            ],
            warnings:
            [
                new WarningInfo
                {
                    Code = "Code",
                    Message = "Message",
                },
            ]);

        var published = McpPublishedResultSerializer.SerializePluginQuery(result, CreateSnapshot());

        published.GetProperty("diagnostics")[0].GetProperty("id").GetString().Should().Be("Id");
        published.GetProperty("warnings")[0].GetProperty("code").GetString().Should().Be("Code");
    }

    [Fact]
    public void GIVEN_CodeActionFailureDetails_WHEN_SerializingFailure_THEN_ShouldPublishDetails()
    {
        var result = CodeActionExecutionResult.Rejected<TestData>(
            new CodeActionExecutionError
            {
                Code = "Code",
                Message = "Message",
            },
            diagnostics:
            [
                new DiagnosticInfo
                {
                    Id = "Id",
                    Message = "Message",
                },
            ],
            warnings:
            [
                new WarningInfo
                {
                    Code = "Code",
                    Message = "Message",
                },
            ]);

        var published = McpPublishedResultSerializer.SerializeCodeActionQuery(result, CreateSnapshot());

        published.GetProperty("diagnostics")[0].GetProperty("id").GetString().Should().Be("Id");
        published.GetProperty("warnings")[0].GetProperty("code").GetString().Should().Be("Code");
    }

#pragma warning disable CA1812 // Payload fixture is consumed through generic serializer metadata.
    private sealed record TestData;
#pragma warning restore CA1812

    private static SnapshotPrecondition CreateSnapshot()
    {
        return WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }
}
