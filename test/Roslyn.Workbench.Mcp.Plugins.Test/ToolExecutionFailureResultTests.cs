namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class ToolExecutionFailureResultTests
{
    [Fact]
    public void GIVEN_FailureResult_WHEN_CreatingTypedPluginResult_THEN_ShouldPreserveOutcomeAndError()
    {
        var result = new ToolExecutionFailureResult
        {
            Outcome = PluginExecutionOutcome.Rejected,
            Error = new PluginExecutionError
            {
                Code = "Rejected",
                Message = "Message",
            },
            RequiredAction = RequiredAction.Retry,
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "CS0002",
                    Message = "Message",
                    Severity = DiagnosticSeverity.Info,
                },
            ],
            Warnings =
            [
                new WarningInfo
                {
                    Code = "Warning",
                    Message = "Message",
                },
            ],
        };

        var pluginResult = result.ToPluginExecutionResult<TestResponse>();

        pluginResult.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        pluginResult.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "Rejected",
            Message = "Message",
        });
        pluginResult.RequiredAction.Should().Be(RequiredAction.Retry);
        pluginResult.Diagnostics.Should().ContainSingle();
        pluginResult.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void GIVEN_UnhandledExceptionDetails_WHEN_CreatingFaultedFailureResult_THEN_ShouldCreateFaultedResult()
    {
        var result = ToolExecutionFailureResult.CreateUnhandledException();

        result.Outcome.Should().Be(PluginExecutionOutcome.Faulted);
        result.Error.Code.Should().Be("UnhandledException");
        result.Error.Message.Should().Be("Tool execution failed.");
        result.Error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
