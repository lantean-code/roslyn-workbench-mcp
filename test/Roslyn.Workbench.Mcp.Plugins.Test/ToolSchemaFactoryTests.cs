using System.Text.Json;

using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class ToolSchemaFactoryTests
{
    [Fact]
    public void GIVEN_SingletonResponseSchema_WHEN_InspectingSharedControlProperties_THEN_ShouldPublishOkValueAndErrorBranches()
    {
        var schema = ToolSchemaFactory.CreateOutputSchema(
            new ToolResponseDescriptor
            {
                Kind = ToolResponseShapeKind.Singleton,
            },
            typeof(TestResponse));
        var variants = schema.GetProperty("oneOf").EnumerateArray().ToArray();
        var successVariant = variants.Single(variant => variant.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());
        var failureVariant = variants.Single(variant => !variant.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        successVariant.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).Should().Contain(["ok", "value"]);
        successVariant.GetProperty("properties").GetProperty("value").GetRawText().Should().Contain("value");
        failureVariant.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).Should().Contain(["ok", "error"]);
        AllowsNull(failureVariant.GetProperty("properties").GetProperty("next")).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_MutationResponseSchema_WHEN_InspectingSuccessBranch_THEN_ShouldPublishMinimalStagedShape()
    {
        var schema = ToolSchemaFactory.CreateOutputSchema(
            new ToolResponseDescriptor
            {
                Kind = ToolResponseShapeKind.Mutation,
            },
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
