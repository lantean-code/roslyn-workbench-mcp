using System.Text.Json;

using AwesomeAssertions;

using Xunit;

namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class ToolSchemaFactoryTests
{
    [Fact]
    public void GIVEN_ToolResultSchema_WHEN_InspectingOptionalEnvelopeProperties_THEN_ShouldAllowNullValues()
    {
        var schema = ToolSchemaFactory.CreateToolResultSchema<TestResponse>();
        var variants = schema.GetProperty("oneOf").EnumerateArray().ToArray();
        var succeededVariant = variants.Single(variant => variant.GetProperty("properties").GetProperty("outcome").GetProperty("const").GetString() == "Succeeded");
        var noChangeVariant = variants.Single(variant => variant.GetProperty("properties").GetProperty("outcome").GetProperty("const").GetString() == "NoChange");
        var rejectedVariant = variants.Single(variant => variant.GetProperty("properties").GetProperty("outcome").GetProperty("const").GetString() == "Rejected");

        AllowsNull(succeededVariant.GetProperty("properties").GetProperty("workspaceEpoch")).Should().BeTrue();
        AllowsNull(succeededVariant.GetProperty("properties").GetProperty("transactionRevision")).Should().BeTrue();
        AllowsNull(succeededVariant.GetProperty("properties").GetProperty("changes")).Should().BeTrue();
        AllowsNull(noChangeVariant.GetProperty("properties").GetProperty("data")).Should().BeTrue();
        AllowsNull(rejectedVariant.GetProperty("properties").GetProperty("requiredAction")).Should().BeTrue();
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
