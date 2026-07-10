using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Test.Protocol.Results;

public sealed class ToolResultEnvelopeSerializerTests
{
    [Fact]
    public void GIVEN_NullFlattenedData_WHEN_Serializing_THEN_ShouldPublishOnlySuccessFlag()
    {
        var result = ToolResultEnvelopeSerializer.CreateFlattenedSuccess<TestData>(null);

        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.EnumerateObject().Should().ContainSingle();
    }

    [Fact]
    public void GIVEN_BlankNestedPropertyName_WHEN_Serializing_THEN_ShouldThrowArgumentException()
    {
        var action = () => ToolResultEnvelopeSerializer.CreateNestedSuccess<TestData>(string.Empty, new TestData());

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_StagedMutationWithoutData_WHEN_Serializing_THEN_ShouldOmitMutationDetails()
    {
        var result = ToolResultEnvelopeSerializer.CreateMutationSuccess(data: null, staged: true);

        result.GetProperty("staged").GetBoolean().Should().BeTrue();
        result.TryGetProperty("summary", out _).Should().BeFalse();
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

        result.GetProperty("summary").GetString().Should().Be("Summary");
        result.TryGetProperty("transaction", out _).Should().BeFalse();
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
