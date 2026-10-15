using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class InputSchemaCompactorTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[]")]
    public void GIVEN_NonObjectSchema_WHEN_Compacting_THEN_ShouldPreserveOriginal(string json)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>(json);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GIVEN_RepeatedSelector_WHEN_Compacting_THEN_ShouldReuseBodyAndKeepContextualDescriptions(bool nullDescription)
    {
        var root = CreateRequest();
        if (nullDescription)
        {
            root["properties"]?["first"]?["description"] = null;
        }

        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        result.GetRawText().Length.Should().BeLessThan(schema.GetRawText().Length);
        var first = result.GetProperty("properties").GetProperty("first");
        var second = result.GetProperty("properties").GetProperty("second");
        first.GetProperty("$ref").GetString().Should().Be("#/$defs/first");
        first.GetProperty("$ref").GetString().Should().Be(second.GetProperty("$ref").GetString());
        JsonElement.DeepEquals(first.GetProperty("description"), schema.GetProperty("properties").GetProperty("first").GetProperty("description")).Should().BeTrue();
        second.GetProperty("description").GetString().Should().Be("Second");
        var body = result.GetProperty("$defs").EnumerateObject().Single().Value;
        body.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        body.GetProperty("required")[0].GetString().Should().Be("projectId");
        body.GetProperty("properties").GetProperty("projectId").GetProperty("minLength").GetInt32().Should().Be(1);
        body.GetProperty("properties").GetProperty("targetFramework").GetProperty("default").GetString().Should().Be("TargetFramework");
        body.TryGetProperty("description", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_SingleOrTinyRepeatedObjects_WHEN_Compacting_THEN_ShouldKeepSmallerOriginal()
    {
        var root = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["first"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
                ["second"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
                ["unique"] = CreateSelector("Unique"),
            },
        };
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Theory]
    [InlineData("$defs")]
    [InlineData("$id")]
    [InlineData("$anchor")]
    [InlineData("$dynamicAnchor")]
    [InlineData("$dynamicRef")]
    [InlineData("$recursiveAnchor")]
    [InlineData("$recursiveRef")]
    public void GIVEN_ExistingDefinitionOrScopeKeyword_WHEN_Compacting_THEN_ShouldLeaveSchemaUntouched(string keyword)
    {
        var root = CreateRequest();
        root[keyword] = "Value";
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://json-schema.org/draft/2020-12/schema", true)]
    [InlineData("http://json-schema.org/draft-07/schema#", false)]
    public void GIVEN_ExplicitDialect_WHEN_Compacting_THEN_ShouldOnlyOptimiseSupportedDialect(string dialect, bool expectedCompaction)
    {
        var root = CreateRequest();
        root["$schema"] = dialect;

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        result.TryGetProperty("$defs", out _).Should().Be(expectedCompaction);
        result.GetProperty("$schema").GetString().Should().Be(dialect);
    }

    [Theory]
    [InlineData("#")]
    [InlineData("#/properties/missing")]
    [InlineData("https://example.invalid/schema")]
    [InlineData("#/type")]
    public void GIVEN_UnresolvableOrRecursiveReference_WHEN_Compacting_THEN_ShouldLeaveSchemaUntouched(string reference)
    {
        var root = CreateRequest();
        root["properties"]?["reference"] = new JsonObject { ["$ref"] = reference };
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GIVEN_UnsupportedReferenceShape_WHEN_Compacting_THEN_ShouldLeaveSchemaUntouched(bool assertionSibling)
    {
        var root = CreateRequest();
        var reference = new JsonObject { ["$ref"] = assertionSibling ? JsonValue.Create("#/properties/first") : null };
        if (assertionSibling)
        {
            reference["maxProperties"] = 1;
        }

        root["properties"]?["reference"] = reference;
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Theory]
    [InlineData("project/name", "project~1name")]
    [InlineData("project~name", "project~0name")]
    [InlineData("project name", "project%20name")]
    public void GIVEN_ExistingEscapedReference_WHEN_Compacting_THEN_ShouldPreserveReferencedBodyAndDescriptions(string propertyName, string encodedName)
    {
        var root = CreateRequest();
        root["properties"]?[propertyName] = CreateSelector("Referenced");
        root["properties"]?["inherited"] = new JsonObject { ["$ref"] = $"#/properties/{encodedName}" };
        root["properties"]?["overridden"] = new JsonObject { ["$ref"] = $"#/properties/{encodedName}", ["description"] = "Overridden" };

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        var properties = result.GetProperty("properties");
        properties.GetProperty("inherited").GetProperty("description").GetString().Should().Be("Referenced");
        properties.GetProperty("overridden").GetProperty("description").GetString().Should().Be("Overridden");
        properties.GetProperty("inherited").GetProperty("$ref").GetString().Should().Be(properties.GetProperty("first").GetProperty("$ref").GetString());
    }

    [Fact]
    public void GIVEN_NullableAndRequiredShapes_WHEN_Compacting_THEN_ShouldKeepDifferentDefinitions()
    {
        var root = CreateRequest();
        var nullable = CreateSelector("Nullable");
        nullable["type"] = new JsonArray("object", "null");
        root["properties"]?["nullableFirst"] = nullable;
        root["properties"]?["nullableSecond"] = nullable.DeepClone();

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        var definitions = result.GetProperty("$defs").EnumerateObject().Select(static property => property.Value).ToArray();
        definitions.Should().HaveCount(2);
        definitions.Should().Contain(static definition => definition.GetProperty("type").ValueKind == JsonValueKind.Array);
        definitions.Should().Contain(static definition => definition.GetProperty("type").ValueKind == JsonValueKind.String);
    }

    [Theory]
    [InlineData("properties")]
    [InlineData("patternProperties")]
    [InlineData("dependentSchemas")]
    [InlineData("allOf")]
    [InlineData("anyOf")]
    [InlineData("oneOf")]
    [InlineData("prefixItems")]
    [InlineData("items")]
    [InlineData("contains")]
    [InlineData("additionalProperties")]
    [InlineData("unevaluatedProperties")]
    [InlineData("unevaluatedItems")]
    [InlineData("propertyNames")]
    [InlineData("not")]
    [InlineData("if")]
    [InlineData("then")]
    [InlineData("else")]
    public void GIVEN_NestedSchemaKeyword_WHEN_Compacting_THEN_ShouldReuseNestedObjects(string keyword)
    {
        var request = CreateRequest();
        var root = new JsonObject { ["type"] = "object" };
        if (keyword is "properties" or "patternProperties" or "dependentSchemas")
        {
            root[keyword] = new JsonObject { ["nested"] = request, ["boolean"] = false };
        }
        else if (keyword is "allOf" or "anyOf" or "oneOf" or "prefixItems")
        {
            root[keyword] = new JsonArray(request, false);
        }
        else
        {
            root[keyword] = request;
        }

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        result.TryGetProperty("$defs", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_SchemaLookingDefaultData_WHEN_Compacting_THEN_ShouldPreserveLiteralData()
    {
        var root = CreateRequest();
        var literal = new JsonObject { ["$ref"] = "NotASchemaReference", ["$id"] = "NotASchemaId", ["properties"] = CreateRequest() };
        root["default"] = literal;

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        JsonElement.DeepEquals(result.GetProperty("default"), JsonSerializer.SerializeToElement(literal)).Should().BeTrue();
        result.TryGetProperty("$defs", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ExcessiveExpansion_WHEN_Compacting_THEN_ShouldLeaveOriginalSchemaUntouched()
    {
        var root = CreateRequest();
        var properties = new JsonObject();
        root["properties"] = properties;
        for (var index = 0; index < 4096; index++)
        {
            properties[$"Property{index}"] = new JsonObject { ["type"] = "string" };
        }

        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Theory]
    [InlineData("items")]
    [InlineData("contains")]
    [InlineData("additionalProperties")]
    [InlineData("unevaluatedProperties")]
    [InlineData("unevaluatedItems")]
    [InlineData("propertyNames")]
    [InlineData("not")]
    [InlineData("if")]
    [InlineData("then")]
    [InlineData("else")]
    public void GIVEN_BooleanChildSchema_WHEN_Compacting_THEN_ShouldPreserveBoolean(string keyword)
    {
        var root = CreateRequest();
        root[keyword] = false;

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        result.GetProperty(keyword).GetBoolean().Should().BeFalse();
        result.TryGetProperty("$defs", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GIVEN_NonStringSchemaMetadata_WHEN_Compacting_THEN_ShouldLeaveOriginalUntouched(bool reference)
    {
        var root = CreateRequest();
        if (reference)
        {
            root["properties"]?["reference"] = new JsonObject { ["$ref"] = 42 };
        }
        else
        {
            root["$schema"] = null;
        }

        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ReferenceWithNullDescription_WHEN_Compacting_THEN_ShouldKeepExplicitNullAnnotation()
    {
        var root = CreateRequest();
        root["properties"]?["reference"] = new JsonObject { ["$ref"] = "#/properties/first", ["description"] = null };

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        result.GetProperty("properties").GetProperty("reference").GetProperty("description").ValueKind.Should().Be(JsonValueKind.Null);
        result.TryGetProperty("$defs", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_DescriptionlessRepeatedSelectors_WHEN_CompactingTwice_THEN_ShouldKeepAnnotationsAbsentAndRemainStable()
    {
        var first = CreateSelector("First");
        first.Remove("description");
        var second = CreateSelector("Second");
        second.Remove("description");
        var root = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["first"] = first, ["second"] = second } };
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);
        var repeated = InputSchemaCompactor.Compact(result);

        result.GetProperty("properties").GetProperty("first").TryGetProperty("description", out _).Should().BeFalse();
        result.TryGetProperty("$defs", out _).Should().BeTrue();
        JsonElement.DeepEquals(repeated, result).Should().BeTrue();
    }

    [Theory]
    [InlineData("itemx")]
    [InlineData("containx")]
    [InlineData("additionalPropertiez")]
    [InlineData("unevaluatedPropertiez")]
    [InlineData("unevaluatedItemx")]
    [InlineData("propertyNamex")]
    [InlineData("nox")]
    [InlineData("ix")]
    [InlineData("thex")]
    [InlineData("elsx")]
    [InlineData("xxxx")]
    [InlineData("x")]
    [InlineData("xxxxxx")]
    [InlineData("xxxxxxx")]
    [InlineData("xxxxxxxxx")]
    [InlineData("xxxxxxxxxxxx")]
    [InlineData("xxxxxxxxxxxxxx")]
    [InlineData("xxxxxxxxxxxxxxx")]
    [InlineData("xxxxxxxxxxxxxxxxx")]
    [InlineData("xxxxxxxxxxxxxxxxxx")]
    [InlineData("xxxxxxxxxxxxxxxxxxx")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxx")]
    public void GIVEN_UnknownKeywordResemblingSchemaKeyword_WHEN_Compacting_THEN_ShouldPreserveItsLiteralValue(string keyword)
    {
        var root = CreateRequest();
        var literal = CreateRequest();
        root[keyword] = literal;

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        JsonElement.DeepEquals(result.GetProperty(keyword), JsonSerializer.SerializeToElement(literal)).Should().BeTrue();
        result.TryGetProperty("$defs", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("project", "project")]
    [InlineData("project/name", "project~1name")]
    [InlineData("project~name", "project~0name")]
    [InlineData("project name", "project%20name")]
    [InlineData("project%name", "project%25name")]
    [InlineData("project#name", "project%23name")]
    [InlineData("", "object")]
    public void GIVEN_PropertyNameHint_WHEN_Compacting_THEN_ShouldNameAndEscapeDefinition(string propertyName, string pointerToken)
    {
        var properties = new JsonObject { [propertyName] = CreateSelector("First"), ["other"] = CreateSelector("Second") };
        var root = new JsonObject { ["type"] = "object", ["properties"] = properties };
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        var expectedName = string.IsNullOrEmpty(propertyName) ? "object" : propertyName;
        result.GetProperty("$defs").TryGetProperty(expectedName, out _).Should().BeTrue();
        result.GetProperty("properties").GetProperty(propertyName).GetProperty("$ref").GetString().Should().Be($"#/$defs/{pointerToken}");
        result.GetRawText().Length.Should().BeLessThan(schema.GetRawText().Length);
    }

    [Fact]
    public void GIVEN_DifferentShapesWithSamePropertyHint_WHEN_Compacting_THEN_ShouldUseStableDistinctNames()
    {
        var left = CreateRequest();
        var right = CreateRequest();
        right["properties"]?["first"]?["title"] = "Alternative";
        right["properties"]?["second"]?["title"] = "Alternative";
        var root = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["left"] = left, ["right"] = right } };
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);
        var repeated = InputSchemaCompactor.Compact(schema);

        result.GetProperty("$defs").EnumerateObject().Select(static property => property.Name).Should().BeEquivalentTo("first", "first2");
        var leftReference = result.GetProperty("properties").GetProperty("left").GetProperty("properties").GetProperty("first").GetProperty("$ref").GetString();
        var rightReference = result.GetProperty("properties").GetProperty("right").GetProperty("properties").GetProperty("first").GetProperty("$ref").GetString();
        leftReference.Should().NotBe(rightReference);
        JsonElement.DeepEquals(result, repeated).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_LongPropertyHint_WHEN_DefinitionNamesWouldIncreaseSize_THEN_ShouldKeepOriginalSchema()
    {
        var propertyName = new string('p', 1000);
        var properties = new JsonObject { [propertyName] = CreateSelector("First"), ["other"] = CreateSelector("Second") };
        var root = new JsonObject { ["type"] = "object", ["properties"] = properties };
        var schema = JsonSerializer.SerializeToElement(root);

        var result = InputSchemaCompactor.Compact(schema);

        JsonElement.DeepEquals(result, schema).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GIVEN_RepeatedArrayItems_WHEN_Compacting_THEN_ShouldUseEnclosingPropertyOrAnonymousFallback(bool anonymous)
    {
        var root = new JsonObject { ["type"] = "object" };
        if (anonymous)
        {
            root["anyOf"] = new JsonArray(CreateSelector("First"), CreateSelector("Second"));
        }
        else
        {
            root["properties"] = new JsonObject
            {
                ["projects"] = new JsonObject { ["type"] = "array", ["items"] = CreateSelector("First") },
                ["otherProjects"] = new JsonObject { ["type"] = "array", ["items"] = CreateSelector("Second") },
            };
        }

        var result = InputSchemaCompactor.Compact(JsonSerializer.SerializeToElement(root));

        result.GetProperty("$defs").EnumerateObject().Single().Name.Should().Be(anonymous ? "object" : "projects");
    }

    private static JsonObject CreateRequest()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["first"] = CreateSelector("First"),
                ["second"] = CreateSelector("Second"),
            },
        };
    }

    private static JsonObject CreateSelector(string description)
    {
        return new JsonObject
        {
            ["description"] = description,
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["projectId"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["description"] = "Workspace-local project identity." },
                ["path"] = new JsonObject { ["type"] = "string", ["description"] = "Workspace-relative project path." },
                ["targetFramework"] = new JsonObject { ["type"] = "string", ["default"] = "TargetFramework" },
            },
            ["required"] = new JsonArray("projectId"),
            ["additionalProperties"] = false,
        };
    }
}
