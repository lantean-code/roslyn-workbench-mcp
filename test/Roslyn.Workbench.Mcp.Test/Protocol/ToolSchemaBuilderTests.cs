using System.Text.Json;
using System.Text.Json.Nodes;

using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Collections;

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
    public void GIVEN_PrimitiveTypeSchema_WHEN_AllowingNull_THEN_ShouldAppendNullType()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "string",
        });

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
        var types = alreadyAllowsNull
            ? new JsonArray("string", "null")
            : new JsonArray("string");
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
    public void GIVEN_ObjectOutput_WHEN_CreatingDirectSchema_THEN_ShouldFlattenObjectProperties()
    {
        var result = ToolSchemaBuilder.CreateDirectOutputSchema(typeof(TestResponse));

        result.GetRawText().Should().Contain("value");
    }

    [Fact]
    public void GIVEN_ScalarOutput_WHEN_CreatingDirectSchema_THEN_ShouldNotPublishObjectProperties()
    {
        var result = ToolSchemaBuilder.CreateDirectOutputSchema(typeof(string));

        result.GetRawText().Should().NotContain("value");
    }

    [Fact]
    public void GIVEN_PrimitiveBoundedCollection_WHEN_CreatingValueSchema_THEN_ShouldPublishItemsAndHasMore()
    {
        var result = ToolSchemaBuilder.CreateValueSchema(typeof(BoundedCollection<string>));

        result.GetRawText().Should().Contain("items");
        result.GetRawText().Should().Contain("hasMore");
    }

    [Fact]
    public void GIVEN_ObjectBoundedCollection_WHEN_CreatingValueSchema_THEN_ShouldPublishItemProperties()
    {
        var result = ToolSchemaBuilder.CreateValueSchema(typeof(BoundedCollection<TestResponse>));

        result.GetRawText().Should().Contain("value");
    }

    [Fact]
    public void GIVEN_ComplexValueType_WHEN_CreatingValueSchema_THEN_ShouldPreserveProperties()
    {
        var result = ToolSchemaBuilder.CreateValueSchema<TypeHierarchyNode>();

        result.GetRawText().Should().Contain("derivedTypes");
    }

    [Fact]
    public void GIVEN_NullableValueType_WHEN_CreatingValueSchema_THEN_ShouldPublishObjectType()
    {
        var result = ToolSchemaBuilder.CreateValueSchema(typeof(TestStruct?));

        result.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void GIVEN_ComplexBoundedCollection_WHEN_CreatingValueSchema_THEN_ShouldPreserveItemProperties()
    {
        var result = ToolSchemaBuilder.CreateValueSchema(typeof(BoundedCollection<TypeHierarchyNode>));

        result.GetRawText().Should().Contain("derivedTypes");
    }

    [Fact]
    public void GIVEN_ResponseWithoutDefinitions_WHEN_CreatingResponseSchema_THEN_ShouldOmitDefinitions()
    {
        var result = ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
            },
            []);

        result.TryGetProperty("$defs", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ResponseComponentsWithDefinitions_WHEN_CreatingResponseSchema_THEN_ShouldMergeDefinitions()
    {
        var component = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["$defs"] = new JsonObject
            {
                ["TestDefinition"] = new JsonObject
                {
                    ["type"] = "string",
                },
                ["NullDefinition"] = null,
            },
        });
        var componentWithoutDefinitions = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "string",
        });

        var result = ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
            },
            [component, componentWithoutDefinitions]);

        result.TryGetProperty("$defs", out var definitions).Should().BeTrue();
        definitions.EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public void GIVEN_ArrayItemSchema_WHEN_CreatingArraySchema_THEN_ShouldPreserveItems()
    {
        var itemSchema = ToolSchemaBuilder.CreateValueSchema<string>();

        var result = ToolSchemaBuilder.CreateArraySchema(itemSchema);

        result["type"]!.GetValue<string>().Should().Be("array");
        result["items"]!.ToJsonString().Should().Be(itemSchema.GetRawText());
    }

    public sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    public readonly record struct TestStruct
    {
        public string Value { get; init; }
    }
}
