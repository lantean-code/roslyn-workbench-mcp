using System.Text.Json;

using Roslyn.Workbench.Mcp.Protocol.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class ToolSchemaFactoryTests
{
    [Fact]
    public void GIVEN_QueryResponseSchema_WHEN_InspectingSharedControlProperties_THEN_ShouldPublishOkDataAndErrorBranches()
    {
        var schema = ToolSchemaFactory.CreateOutputSchema(
            PublishedToolKind.Query,
            typeof(TestResponse));
        var variants = schema.GetProperty("oneOf").EnumerateArray().ToArray();
        var successVariant = variants.Single(variant => variant.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());
        var failureVariant = variants.Single(variant => !variant.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        successVariant.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).Should().Contain(["ok", "data"]);
        successVariant.GetProperty("properties").GetProperty("data").GetRawText().Should().Contain("value");
        failureVariant.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).Should().Contain(["ok", "error"]);
        AllowsNull(failureVariant.GetProperty("properties").GetProperty("next")).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_MutationResponseSchema_WHEN_InspectingSuccessBranch_THEN_ShouldPublishMinimalStagedShape()
    {
        var schema = ToolSchemaFactory.CreateOutputSchema(
            PublishedToolKind.Mutation,
            typeof(MutationData));
        var successVariant = schema.GetProperty("oneOf")
            .EnumerateArray()
            .Single(variant => variant.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        successVariant.GetProperty("properties").TryGetProperty("staged", out _).Should().BeTrue();
        successVariant.GetProperty("properties").TryGetProperty("summary", out _).Should().BeTrue();
        successVariant.GetProperty("properties").TryGetProperty("transaction", out _).Should().BeTrue();
        successVariant.GetRawText().Should().NotContain("changes");
        successVariant.GetRawText().Should().NotContain("preview");
    }

    [Fact]
    public void GIVEN_NullResponseType_WHEN_CreatingOutputSchema_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => ToolSchemaFactory.CreateOutputSchema(PublishedToolKind.Query, null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_ToolResultSchema_WHEN_Creating_THEN_ShouldPublishEveryOutcomeVariant()
    {
        var schema = ToolSchemaFactory.CreateToolResultSchema<TestResponse>();

        var outcomes = schema.GetProperty("oneOf")
            .EnumerateArray()
            .Select(variant => variant.GetProperty("properties").GetProperty("outcome").GetProperty("const").GetString())
            .ToArray();
        outcomes.Should().Equal("Succeeded", "NoChange", "Rejected", "Conflict", "Faulted");
        schema.GetRawText().Should().Contain("diagnostics");
        schema.GetRawText().Should().Contain("warnings");
        schema.GetRawText().Should().Contain("requiredAction");
    }

    private static bool AllowsNull(JsonElement propertySchema)
    {
        if (!propertySchema.TryGetProperty("type", out var typeProperty))
        {
            return false;
        }

        return typeProperty.ValueKind switch
        {
            JsonValueKind.String => typeProperty.GetString() == "null",
            JsonValueKind.Array => typeProperty.EnumerateArray().Any(value => value.GetString() == "null"),
            _ => false,
        };
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
