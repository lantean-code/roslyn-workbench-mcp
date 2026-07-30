namespace Roslyn.Workbench.Mcp.Test.Protocol.Results;

public sealed class ToolResultEnvelopeSerializerTests
{
    [Fact]
    public void GIVEN_NullData_WHEN_SerializingSuccess_THEN_ShouldPublishNullData()
    {
        var result = ToolResultEnvelopeSerializer.CreateSuccess<TestData>(null);

        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.GetProperty("data").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public void GIVEN_StagedMutationWithoutData_WHEN_Serializing_THEN_ShouldOmitMutationDetails()
    {
        var result = ToolResultEnvelopeSerializer.CreateMutationSuccess(data: null, staged: true);

        var data = result.GetProperty("data");
        data.GetProperty("staged").GetBoolean().Should().BeTrue();
        data.TryGetProperty("summary", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_StagedMutationWithoutTransaction_WHEN_Serializing_THEN_ShouldPublishSummaryWithoutTransaction()
    {
        var result = ToolResultEnvelopeSerializer.CreateMutationSuccess(
            new MutationData
            {
                Summary = "Summary",
            },
            staged: true);

        var data = result.GetProperty("data");
        data.GetProperty("summary").GetString().Should().Be("Summary");
        data.TryGetProperty("transaction", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NullFailureDetails_WHEN_Serializing_THEN_ShouldPublishNullErrorWithoutNextAction()
    {
        var result = ToolResultEnvelopeSerializer.CreateFailure(error: null, requiredAction: null);

        result.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.GetProperty("error").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        result.TryGetProperty("next", out _).Should().BeFalse();
        result.TryGetProperty("diagnostics", out _).Should().BeFalse();
        result.TryGetProperty("warnings", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_FailureDiagnosticsAndWarnings_WHEN_Serializing_THEN_ShouldPublishDetails()
    {
        var diagnostic = new DiagnosticInfo
        {
            Id = "Id",
            Severity = DiagnosticSeverity.Error,
            Message = "Message",
        };
        var warning = new WarningInfo
        {
            Code = "Code",
            Message = "Message",
        };

        var result = ToolResultEnvelopeSerializer.CreateFailure(
            new ToolError
            {
                Code = "Code",
                Message = "Message",
            },
            RequiredAction.Retry,
            [diagnostic],
            [warning]);

        var publishedDiagnostic = result.GetProperty("diagnostics").EnumerateArray().Should().ContainSingle().Subject;
        publishedDiagnostic.GetProperty("id").GetString().Should().Be("Id");
        publishedDiagnostic.GetProperty("severity").GetString().Should().Be("Error");
        publishedDiagnostic.GetProperty("message").GetString().Should().Be("Message");
        var publishedWarning = result.GetProperty("warnings").EnumerateArray().Should().ContainSingle().Subject;
        publishedWarning.GetProperty("code").GetString().Should().Be("Code");
        publishedWarning.GetProperty("message").GetString().Should().Be("Message");
    }

#pragma warning disable CA1812 // Payload fixture is consumed through generic serializer metadata.
    private sealed record TestData
    {
        public string Value { get; init; } = string.Empty;
    }
#pragma warning restore CA1812
}
