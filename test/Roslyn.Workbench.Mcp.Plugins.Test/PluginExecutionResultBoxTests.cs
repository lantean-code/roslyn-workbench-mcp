namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginExecutionResultBoxTests
{
    [Fact]
    public void GIVEN_PluginExecutionResult_WHEN_BoxingResult_THEN_ShouldPreserveOutcomeAndPayload()
    {
        var result = PluginExecutionResult<TestResponse>.Success(
            new TestResponse
            {
                Value = "Value",
            },
            changes: new ChangeSummary
            {
                Added = [new DocumentChange()],
            },
            diagnostics:
            [
                new DiagnosticInfo
                {
                    Id = "CS0001",
                    Message = "Message",
                    Severity = DiagnosticSeverity.Warning,
                },
            ],
            warnings:
            [
                new WarningInfo
                {
                    Code = "Warning",
                    Message = "Message",
                },
            ]);

        var box = PluginExecutionResultBox.From(result);

        box.Outcome.Should().Be(ToolOutcome.Succeeded);
        box.Data.Should().BeEquivalentTo(new TestResponse
        {
            Value = "Value",
        });
        box.Changes.Should().BeEquivalentTo(new ChangeSummary
        {
            Added = [new DocumentChange()],
        });
        box.Diagnostics.Should().ContainSingle();
        box.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void GIVEN_ToolResult_WHEN_BoxingResult_THEN_ShouldPreserveOutcomeAndPayload()
    {
        var result = ToolResult<TestResponse>.Rejected(
            new ToolError
            {
                Code = "Rejected",
                Message = "Message",
            },
            requiredAction: RequiredAction.Retry,
            diagnostics:
            [
                new DiagnosticInfo
                {
                    Id = "CS0002",
                    Message = "Message",
                    Severity = DiagnosticSeverity.Info,
                },
            ]);

        var box = PluginExecutionResultBox.From(result);

        box.Outcome.Should().Be(ToolOutcome.Rejected);
        box.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "Rejected",
            Message = "Message",
        });
        box.RequiredAction.Should().Be(RequiredAction.Retry);
        box.Diagnostics.Should().ContainSingle();
    }

    [Fact]
    public void GIVEN_UnhandledExceptionDetails_WHEN_CreatingFaultedBox_THEN_ShouldCreateFaultedResult()
    {
        var box = PluginExecutionResultBox.CreateUnhandledException();

        box.Outcome.Should().Be(ToolOutcome.Faulted);
        box.Error.Should().NotBeNull();
        box.Error!.Code.Should().Be("UnhandledException");
        box.Error.Message.Should().Be("Tool execution failed.");
        box.Error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
