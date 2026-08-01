using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

#pragma warning disable CA2263 // These tests verify runtime response-type dispatch through the non-generic schema-provider overload.
public sealed class ToolSchemaFactoryTests
{
    private readonly Mock<IMcpSdkSchemaProvider> _schemaProvider;
    private readonly ToolSchemaFactory _target;

    public ToolSchemaFactoryTests()
    {
        _schemaProvider = new Mock<IMcpSdkSchemaProvider>();
        _schemaProvider
            .Setup(item => item.GetValueSchema<ToolError>())
            .Returns(CreateObjectSchema("code"));

        _schemaProvider
            .Setup(item => item.GetValueSchema<RequiredAction>())
            .Returns(CreatePrimitiveSchema("string"));

        _target = new ToolSchemaFactory(_schemaProvider.Object);
    }

    [Fact]
    public void GIVEN_InputContract_WHEN_CreatingInputSchema_THEN_ShouldReturnProviderSchema()
    {
        var expected = CreateObjectSchema("value");
        _schemaProvider
            .Setup(item => item.GetInputSchema<TestRequest>())
            .Returns(expected);

        var result = _target.CreateInputSchema<TestRequest>();

        result.GetRawText().Should().Be(expected.GetRawText());
        _schemaProvider.Verify(item => item.GetInputSchema<TestRequest>(), Times.Once);
    }

    [Fact]
    public void GIVEN_DirectResponseContract_WHEN_CreatingSchemaTwice_THEN_ShouldCacheComposedSchema()
    {
        _schemaProvider
            .Setup(item => item.GetValueSchema(typeof(TestResponse)))
            .Returns(CreateObjectSchema("value"));

        var first = _target.CreateDirectOutputSchema(typeof(TestResponse));
        var second = _target.CreateDirectOutputSchema(typeof(TestResponse));

        first.GetRawText().Should().Be(second.GetRawText());
        var successVariant = first.GetProperty("oneOf")
            .EnumerateArray()
            .Single(variant => variant.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        successVariant.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).Should().Contain(["ok", "data"]);
        successVariant.GetProperty("properties").GetProperty("data").GetRawText().Should().Contain("value");
        _schemaProvider.Verify(item => item.GetValueSchema(typeof(TestResponse)), Times.Once);
        _schemaProvider.Verify(item => item.GetValueSchema<ToolError>(), Times.Once);
        _schemaProvider.Verify(item => item.GetValueSchema<RequiredAction>(), Times.Once);
    }

    [Fact]
    public void GIVEN_QueryResponseSchema_WHEN_InspectingSharedControlProperties_THEN_ShouldPublishOkDataAndErrorBranches()
    {
        _schemaProvider
            .Setup(item => item.GetValueSchema(typeof(TestResponse)))
            .Returns(CreateObjectSchema("value"));

        var schema = _target.CreateOutputSchema(PublishedToolKind.Query, typeof(TestResponse));
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
        var schema = _target.CreateOutputSchema(PublishedToolKind.Mutation, typeof(MutationData));
        var successVariant = schema.GetProperty("oneOf")
            .EnumerateArray()
            .Single(variant => variant.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        var data = successVariant.GetProperty("properties").GetProperty("data");
        successVariant.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).Should().Contain(["ok", "data"]);
        data.GetProperty("properties").TryGetProperty("staged", out _).Should().BeTrue();
        data.GetProperty("properties").TryGetProperty("summary", out _).Should().BeTrue();
        data.GetProperty("properties").TryGetProperty("transaction", out _).Should().BeTrue();
        successVariant.GetRawText().Should().NotContain("changes");
        successVariant.GetRawText().Should().NotContain("preview");
        _schemaProvider.Verify(item => item.GetValueSchema(typeof(MutationData)), Times.Never);
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

    private static JsonElement CreateObjectSchema(string propertyName)
    {
        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { propertyName },
            properties = new Dictionary<string, object>
            {
                [propertyName] = new
                {
                    type = "string",
                },
            },
        });
    }

    private static JsonElement CreatePrimitiveSchema(string type)
    {
        return JsonSerializer.SerializeToElement(new
        {
            type,
        });
    }

#pragma warning disable CA1812 // Contract fixtures are consumed as schema type metadata without object construction.
    private sealed record TestRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

#pragma warning restore CA1812
}
#pragma warning restore CA2263
