using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginTransportSchemaPreflightTests
{
    [Fact]
    public void GIVEN_RequestSchemaGenerationFails_WHEN_Preflighting_THEN_ShouldReturnToolDiagnostic()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        schemaFactory
            .Setup(factory => factory.CreateInputSchemaForType(typeof(TestRequest)))
            .Throws(new NotSupportedException("Unsupported contract."));
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool();

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(diagnostic =>
            diagnostic.Id == PluginDiagnosticIds.ToolSchema
            && diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Message.Contains("plugin-tool", StringComparison.Ordinal)
            && diagnostic.Message.Contains("request", StringComparison.Ordinal)
            && diagnostic.Message.Contains(nameof(NotSupportedException), StringComparison.Ordinal)
            && !diagnostic.Message.Contains("Unsupported contract", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_ValidContracts_WHEN_Preflighting_THEN_ShouldValidateRequestAndResponseContracts()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool();

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeTrue();
        result.Failures.Should().BeNull();
        schemaFactory.Verify(factory => factory.CreateInputSchemaForType(typeof(TestRequest)), Times.Once);
        schemaFactory.Verify(
            factory => factory.CreateOutputSchema(PublishedToolKind.Query, typeof(TestResponse)),
            Times.Once);
    }

    [Fact]
    public void GIVEN_ScalarResponseContract_WHEN_Preflighting_THEN_ShouldRejectTool()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool(typeof(string));

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(diagnostic =>
            diagnostic.Id == PluginDiagnosticIds.ToolSchema
            && diagnostic.Message.Contains("response", StringComparison.Ordinal)
            && diagnostic.Message.Contains(nameof(JsonTypeInfoKind.None), StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_CollectionResponseContract_WHEN_Preflighting_THEN_ShouldRejectTool()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool(typeof(string[]));

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(diagnostic =>
            diagnostic.Id == PluginDiagnosticIds.ToolSchema
            && diagnostic.Message.Contains(nameof(JsonTypeInfoKind.Enumerable), StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_DictionaryResponseContract_WHEN_Preflighting_THEN_ShouldRejectTool()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool(typeof(TestDictionaryResponse));

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(diagnostic =>
            diagnostic.Id == PluginDiagnosticIds.ToolSchema
            && diagnostic.Message.Contains(nameof(JsonTypeInfoKind.Dictionary), StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_MutationContract_WHEN_Preflighting_THEN_ShouldValidateMutationOutputSchema()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool(typeof(MutationData), ToolKind.Mutation);

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeTrue();
        result.Failures.Should().BeNull();
        schemaFactory.Verify(
            factory => factory.CreateOutputSchema(PublishedToolKind.Mutation, typeof(MutationData)),
            Times.Once);
    }

    [Fact]
    public void GIVEN_CustomConverterResponseContract_WHEN_Preflighting_THEN_ShouldRejectTool()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool(typeof(CustomConverterResponse));

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(diagnostic =>
            diagnostic.Id == PluginDiagnosticIds.ToolSchema
            && diagnostic.Message.Contains(nameof(JsonTypeInfoKind.None), StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_ResponseSchemaGenerationFails_WHEN_Preflighting_THEN_ShouldReturnSanitisedToolDiagnostic()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        schemaFactory
            .Setup(factory => factory.CreateOutputSchema(PublishedToolKind.Query, typeof(TestResponse)))
            .Throws(new NotSupportedException("Sensitive contract details."));
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool();

        var result = target.Preflight([tool]);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(diagnostic =>
            diagnostic.Id == PluginDiagnosticIds.ToolSchema
            && diagnostic.Message.Contains("response", StringComparison.Ordinal)
            && diagnostic.Message.Contains(nameof(NotSupportedException), StringComparison.Ordinal)
            && !diagnostic.Message.Contains("Sensitive contract details", StringComparison.Ordinal));
    }

    private static PreparedPluginTool CreatePreparedTool(Type? responseType = null, ToolKind toolKind = ToolKind.Query)
    {
        return new PreparedPluginTool
        {
            HandlerType = typeof(object),
            HandlerContract = typeof(object),
            Tool = new RegisteredTool
            {
                Plugin = new PluginMetadata
                {
                    PluginId = "plugin",
                },
                Metadata = new ToolRegistrationMetadata
                {
                    Name = "plugin-tool",
                },
                Kind = toolKind,
                RequestType = typeof(TestRequest),
                ResponseType = responseType ?? typeof(TestResponse),
            },
        };
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The request type is deliberately consumed as schema metadata rather than instantiated.")]
    private sealed record TestRequest
    {
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The response type is deliberately consumed as schema metadata rather than instantiated.")]
    private sealed record TestResponse : IQueryResponse
    {
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The response type is deliberately consumed as serializer metadata rather than instantiated.")]
    private sealed class TestDictionaryResponse : Dictionary<string, string>, IQueryResponse
    {
    }

    [JsonConverter(typeof(CustomConverterResponseJsonConverter))]
    private sealed record CustomConverterResponse : IQueryResponse
    {
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json constructs the converter through response contract metadata.")]
    private sealed class CustomConverterResponseJsonConverter : JsonConverter<CustomConverterResponse>
    {
        public override CustomConverterResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new CustomConverterResponse();
        }

        public override void Write(Utf8JsonWriter writer, CustomConverterResponse value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
    }
}
