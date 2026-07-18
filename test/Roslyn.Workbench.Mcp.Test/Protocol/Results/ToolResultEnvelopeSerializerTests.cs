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
    }

    public sealed record TestData
    {
        public string Value { get; init; } = string.Empty;
    }
}
