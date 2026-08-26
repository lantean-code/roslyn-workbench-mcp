using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class InputSchemaExporterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GIVEN_EmptyObjectRequest_WHEN_ExtractingRequestSchema_THEN_ShouldCloseObject(bool includeEmptyProperties)
    {
        var request = new JsonObject
        {
            ["type"] = "object",
        };
        if (includeEmptyProperties)
        {
            request["properties"] = new JsonObject();
        }

        var result = InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        result.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ExplicitlyOpenEmptyObjectRequest_WHEN_ExtractingRequestSchema_THEN_ShouldPreserveOpenObject()
    {
        var request = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = true,
        };

        var result = InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        result.GetProperty("additionalProperties").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ObjectRequestWithProperties_WHEN_ExtractingRequestSchema_THEN_ShouldPreserveSdkObjectShape()
    {
        var request = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["value"] = new JsonObject { ["type"] = "string" },
            },
        };

        var result = InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        result.TryGetProperty("additionalProperties", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NonStringRequestType_WHEN_ExtractingRequestSchema_THEN_ShouldPreserveSdkSchema()
    {
        var request = new JsonObject
        {
            ["type"] = 1,
        };

        var result = InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        result.GetProperty("type").GetInt32().Should().Be(1);
        result.TryGetProperty("additionalProperties", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_RequestReferencesAndDefinitions_WHEN_ExtractingRequestSchema_THEN_ShouldRebaseAndPreserveReferences()
    {
        var root = CreateRoot(new JsonObject
        {
            ["type"] = new JsonArray("object", "null"),
            ["properties"] = new JsonObject
            {
                ["self"] = CreateReference("#/properties/request"),
                ["nested"] = CreateReference("#/properties/request/properties/value"),
                ["value"] = new JsonObject { ["type"] = "string" },
                ["definition"] = CreateReference("#/$defs/Definition"),
                ["definitions"] = CreateReference("#/$defs"),
                ["external"] = CreateReference("schema.json#/Definition"),
                ["anchor"] = CreateReference("#Definition"),
            },
        });

        var definitions = new JsonObject
        {
            ["Definition"] = new JsonObject { ["type"] = "integer" },
        };
        root = AddDefinitions(root, definitions);

        var result = InputSchemaExporter.ExtractRequestSchema(root);
        var properties = result.GetProperty("properties");

        result.GetProperty("type").GetString().Should().Be("object");
        properties.GetProperty("self").GetProperty("$ref").GetString().Should().Be("#");
        properties.GetProperty("nested").GetProperty("$ref").GetString().Should().Be("#/properties/value");
        properties.GetProperty("definition").GetProperty("$ref").GetString().Should().Be("#/$defs/Definition");
        properties.GetProperty("definitions").GetProperty("$ref").GetString().Should().Be("#/$defs");
        properties.GetProperty("external").GetProperty("$ref").GetString().Should().Be("schema.json#/Definition");
        properties.GetProperty("anchor").GetProperty("$ref").GetString().Should().Be("#Definition");
        result.GetProperty("$defs").TryGetProperty("Definition", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_EscapedAndPercentEncodedPointers_WHEN_ExtractingRequestSchema_THEN_ShouldResolveTargets()
    {
        var request = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["a/b~c"] = new JsonObject { ["type"] = "string" },
                ["a b"] = new JsonObject { ["type"] = "string" },
                ["escaped"] = CreateReference("#/properties/request/properties/a~1b~0c"),
                ["encoded"] = CreateReference("#/properties/request/properties/a%20b"),
            },
        };

        var result = InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));
        var properties = result.GetProperty("properties");

        properties.GetProperty("escaped").GetProperty("$ref").GetString().Should().Be("#/properties/a~1b~0c");
        properties.GetProperty("encoded").GetProperty("$ref").GetString().Should().Be("#/properties/a%20b");
    }

    [Fact]
    public void GIVEN_ArrayPointer_WHEN_ExtractingRequestSchema_THEN_ShouldResolveArrayElement()
    {
        var request = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["choice"] = new JsonObject
                {
                    ["oneOf"] = new JsonArray(
                        new JsonObject { ["type"] = "string" },
                        new JsonObject { ["type"] = "integer" }),
                },
                ["selected"] = CreateReference("#/properties/request/properties/choice/oneOf/1"),
            },
        };

        var result = InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        result.GetProperty("properties").GetProperty("selected").GetProperty("$ref").GetString()
            .Should().Be("#/properties/choice/oneOf/1");
    }

    [Fact]
    public void GIVEN_ReferenceOutsideRequest_WHEN_ExtractingRequestSchema_THEN_ShouldRejectReference()
    {
        var request = CreateReference("#/properties/response");
        request["type"] = "object";

        var action = () => InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*escapes the extracted request schema*");
    }

    [Theory]
    [InlineData("#/properties/request/properties/missing")]
    [InlineData("#/properties/request/properties/value~")]
    [InlineData("#/properties/request/properties/value~2")]
    [InlineData("#/properties/request/properties/choices/not-an-index")]
    [InlineData("#/properties/request/properties/choices/00")]
    [InlineData("#/properties/request/properties/choices/2")]
    [InlineData("#/properties/request/properties/choices/10")]
    [InlineData("#/properties/request/properties/value/child")]
    public void GIVEN_UnresolvedRequestReference_WHEN_ExtractingRequestSchema_THEN_ShouldRejectReference(string reference)
    {
        var request = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["value"] = new JsonObject { ["type"] = "string" },
                ["choices"] = new JsonArray(new JsonObject { ["type"] = "string" }),
                ["invalid"] = CreateReference(reference),
            },
        };

        var action = () => InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*contains unresolved local reference*");
    }

    [Fact]
    public void GIVEN_RequestWithoutDefinitions_WHEN_ExtractingRequestSchema_THEN_ShouldNotAddDefinitions()
    {
        var request = new JsonObject
        {
            ["type"] = new JsonArray(null, "string", "null"),
        };

        var result = InputSchemaExporter.ExtractRequestSchema(CreateRoot(request));

        result.GetProperty("type").EnumerateArray().Select(static item => item.ValueKind).Should().Equal(JsonValueKind.Null, JsonValueKind.String, JsonValueKind.String);
        result.TryGetProperty("$defs", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NonObjectRequestSchema_WHEN_ExtractingRequestSchema_THEN_ShouldRejectSchema()
    {
        var root = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["request"] = "string",
            },
        });

        var action = () => InputSchemaExporter.ExtractRequestSchema(root);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*was not a JSON object*");
    }

    private static JsonElement CreateRoot(JsonObject request)
    {
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["request"] = request,
                ["response"] = new JsonObject { ["type"] = "string" },
            },
        });
    }

    private static JsonElement AddDefinitions(JsonElement root, JsonObject definitions)
    {
        var rootNode = JsonNode.Parse(root.GetRawText())?.AsObject()
            ?? throw new InvalidOperationException("The test root was not a JSON object.");

        rootNode["$defs"] = definitions;
        return JsonSerializer.SerializeToElement(rootNode);
    }

    private static JsonObject CreateReference(string reference)
    {
        return new JsonObject
        {
            ["$ref"] = reference,
        };
    }
}
