using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class ToolSchemaBuilderTests
{
    [Fact]
    public void GIVEN_EmptyPrimitiveType_WHEN_CreatingNullablePrimitiveSchema_THEN_ShouldThrowArgumentException()
    {
        var action = () => ToolSchemaBuilder.CreateNullablePrimitiveSchema(string.Empty);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_PrimitiveType_WHEN_CreatingNullablePrimitiveSchema_THEN_ShouldIncludePrimitiveAndNull()
    {
        var result = ToolSchemaBuilder.CreateNullablePrimitiveSchema("string");

        result["type"]!.AsArray().Select(item => item!.GetValue<string>()).Should().Equal("string", "null");
    }

    [Fact]
    public void GIVEN_PrimitiveTypeSchema_WHEN_AllowingNull_THEN_ShouldAppendNullType()
    {
        var schema = CreatePrimitiveSchema("string");

        var result = ToolSchemaBuilder.AllowNull(schema).AsObject();

        result["type"]!.AsArray().Select(item => item!.GetValue<string>()).Should().Equal("string", "null");
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void GIVEN_ArrayTypeSchema_WHEN_AllowingNull_THEN_ShouldContainNullOnce(
        bool alreadyAllowsNull,
        int expectedCount)
    {
        var types = new JsonArray("string");
        if (alreadyAllowsNull)
        {
            types.Add("null");
        }

        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = types,
        });

        var result = ToolSchemaBuilder.AllowNull(schema).AsObject();

        result["type"]!.AsArray().Should().HaveCount(expectedCount);
        result["type"]!.AsArray().Count(item => item!.GetValue<string>() == "null").Should().Be(1);
    }

    [Fact]
    public void GIVEN_ArrayTypeSchemaWithNullNode_WHEN_AllowingNull_THEN_ShouldAppendNullType()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = new JsonArray("string", null),
        });

        var result = ToolSchemaBuilder.AllowNull(schema).AsObject();

        result["type"]!.AsArray().Count(item => item is not null && item.GetValue<string>() == "null").Should().Be(1);
    }

    [Fact]
    public void GIVEN_SchemaWithoutType_WHEN_AllowingNull_THEN_ShouldCreateAnyOf()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["const"] = "Value",
        });

        var result = ToolSchemaBuilder.AllowNull(schema).AsObject();

        result["anyOf"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public void GIVEN_ObjectOutput_WHEN_CreatingDirectSchema_THEN_ShouldPublishObjectUnderData()
    {
        var valueSchemaNode = JsonNode.Parse(CreateObjectSchema("value").GetRawText())!.AsObject();
        valueSchemaNode["properties"]!.AsObject()["optional"] = null;
        var valueSchema = JsonSerializer.SerializeToElement(valueSchemaNode);

        var result = ToolSchemaBuilder.CreateDirectOutputSchema(
            valueSchema,
            CreateObjectSchema("code"),
            CreatePrimitiveSchema("string"),
            CreateObjectSchema("workspaceId"));

        var success = GetSuccessVariant(result);

        var data = success.GetProperty("properties").GetProperty("data");
        data.GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
        data.GetProperty("properties").GetProperty("optional").ValueKind.Should().Be(JsonValueKind.Null);
        AllowsNull(data).Should().BeTrue();
        success.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Contain(["ok", "data"]);
        var successProperties = success.GetProperty("properties");
        successProperties.GetProperty("ok").GetProperty("description").GetString().Should().Be("Whether the tool invocation succeeded.");
        successProperties.GetProperty("data").GetProperty("description").GetString().Should().Be("Tool-specific result payload.");
        successProperties.GetProperty("snapshot").GetProperty("description").GetString().Should().Be("Exact immutable workspace snapshot associated with the result, when available.");

        var failure = GetFailureVariant(result);
        var failureProperties = failure.GetProperty("properties");
        failureProperties.GetProperty("error").GetProperty("description").GetString().Should().Be("Structured error details when the invocation failed.");
        failureProperties.GetProperty("continuation").GetProperty("description").GetString().Should().Be("Action the agent should take before retrying or continuing.");
    }

    [Fact]
    public void GIVEN_ScalarOutput_WHEN_CreatingDirectSchema_THEN_ShouldPublishScalarUnderData()
    {
        var result = ToolSchemaBuilder.CreateDirectOutputSchema(
            CreatePrimitiveSchema("string"),
            CreateObjectSchema("code"),
            CreatePrimitiveSchema("string"),
            CreateObjectSchema("workspaceId"));

        var success = GetSuccessVariant(result);

        success.GetProperty("properties").EnumerateObject().Select(item => item.Name).Should().Equal("ok", "data", "snapshot");
        var data = success.GetProperty("properties").GetProperty("data");
        AllowsNull(data).Should().BeTrue();
        data.GetProperty("type").EnumerateArray().Select(static item => item.GetString()).Should().Equal("string", "null");
        success.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("ok", "data");
    }

    [Fact]
    public void GIVEN_ResponseWithoutDefinitions_WHEN_CreatingResponseSchema_THEN_ShouldOmitDefinitions()
    {
        var result = ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
            },
            [],
            CreateObjectSchema("code"),
            CreatePrimitiveSchema("string"));

        result.TryGetProperty("$defs", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ResponseComponentsWithDefinitions_WHEN_CreatingResponseSchema_THEN_ShouldMergeDefinitions()
    {
        var component = CreateSchemaWithDefinitions("ComponentDefinition");
        var error = CreateSchemaWithDefinitions("ErrorDefinition");
        var continuation = CreateSchemaWithDefinitions("ContinuationDefinition");

        var result = ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
            },
            [component, CreatePrimitiveSchema("string")],
            error,
            continuation);

        var definitions = result.GetProperty("$defs");
        definitions.TryGetProperty("ComponentDefinition", out _).Should().BeTrue();
        definitions.TryGetProperty("ErrorDefinition", out _).Should().BeTrue();
        definitions.TryGetProperty("ContinuationDefinition", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ItemSchemaWithoutDefinitions_WHEN_CreatingBoundedCollectionSchema_THEN_ShouldPublishBoundedCollectionProperties()
    {
        var result = ToolSchemaBuilder.CreateBoundedCollectionSchema(CreatePrimitiveSchema("string"));

        result.GetProperty("properties").GetProperty("items").GetProperty("type").GetString().Should().Be("array");
        result.GetProperty("properties").GetProperty("hasMore").GetProperty("type").GetString().Should().Be("boolean");
        result.GetProperty("properties").GetProperty("totalCount").GetProperty("type").GetString().Should().Be("integer");
        result.GetProperty("properties").GetProperty("totalCount").GetProperty("minimum").GetInt32().Should().Be(0);
        result.GetProperty("properties").GetProperty("items").GetProperty("description").GetString().Should().Be("Items returned in this page.");
        result.GetProperty("properties").GetProperty("hasMore").GetProperty("description").GetString().Should().Be("Whether additional items were available beyond this page.");
        result.GetProperty("properties").GetProperty("totalCount").GetProperty("description").GetString().Should().Be("Complete result count, when available without additional expensive work.");
        result.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).Should().NotContain("totalCount");
        result.TryGetProperty("$defs", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ItemSchemaWithDefinitions_WHEN_CreatingBoundedCollectionSchema_THEN_ShouldPreserveDefinitions()
    {
        var result = ToolSchemaBuilder.CreateBoundedCollectionSchema(CreateSchemaWithDefinitions("ItemDefinition"));

        result.GetProperty("$defs").TryGetProperty("ItemDefinition", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ArrayItemSchema_WHEN_CreatingArraySchema_THEN_ShouldPreserveItems()
    {
        var itemSchema = CreatePrimitiveSchema("string");

        var result = ToolSchemaBuilder.CreateArraySchema(itemSchema);

        result["type"]!.GetValue<string>().Should().Be("array");
        result["items"]!.ToJsonString().Should().Be(itemSchema.GetRawText());
    }

    [Fact]
    public void GIVEN_NullableObjectExportWithRootDefinitions_WHEN_Normalizing_THEN_ShouldUseObjectTypeAndCopyDefinitions()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = new JsonArray(null, "object", "null"),
        });

        var root = CreateSchemaWithDefinitions("Definition");

        var result = ToolSchemaBuilder.NormalizeExportedSchema(schema, root);

        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("$defs").TryGetProperty("Definition", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_SimpleExportWithoutDefinitions_WHEN_Normalizing_THEN_ShouldPreserveSchema()
    {
        var schema = CreatePrimitiveSchema("string");

        var result = ToolSchemaBuilder.NormalizeExportedSchema(schema, CreatePrimitiveSchema("object"));

        result.GetRawText().Should().Be(schema.GetRawText());
    }

    [Fact]
    public void GIVEN_NonObjectTypeArray_WHEN_Normalizing_THEN_ShouldPreserveTypeArray()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = new JsonArray("string", "null"),
        });

        var result = ToolSchemaBuilder.NormalizeExportedSchema(schema, CreatePrimitiveSchema("object"));

        result.GetProperty("type").EnumerateArray().Select(item => item.GetString()).Should().Equal("string", "null");
    }

    [Fact]
    public void GIVEN_NonObjectSchema_WHEN_Composing_THEN_ShouldRejectInvalidSchema()
    {
        var schema = JsonSerializer.SerializeToElement("string");

        var action = () => ToolSchemaBuilder.AllowNull(schema);

        action.Should().Throw<InvalidOperationException>();
    }

    private static JsonElement GetSuccessVariant(JsonElement schema)
    {
        return schema.GetProperty("oneOf")
            .EnumerateArray()
            .Single(item => item.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());
    }

    private static JsonElement GetFailureVariant(JsonElement schema)
    {
        return schema.GetProperty("oneOf")
            .EnumerateArray()
            .Single(item => !item.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());
    }

    private static bool AllowsNull(JsonElement schema)
    {
        var type = schema.GetProperty("type");
        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(static item => string.Equals(item.GetString(), "null", StringComparison.Ordinal));
    }

    private static JsonElement CreateObjectSchema(string propertyName)
    {
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray(propertyName, null),
            ["properties"] = new JsonObject
            {
                [propertyName] = new JsonObject
                {
                    ["type"] = "string",
                },
            },
        });
    }

    private static JsonElement CreatePrimitiveSchema(string type)
    {
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = type,
        });
    }

    private static JsonElement CreateSchemaWithDefinitions(string definitionName)
    {
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["$defs"] = new JsonObject
            {
                [definitionName] = new JsonObject
                {
                    ["type"] = "string",
                },
                ["NullDefinition"] = null,
            },
        });
    }
}
