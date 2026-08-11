using System.Diagnostics.CodeAnalysis;

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

        var result = target.Preflight([tool], ToolOutputSchemaMode.Omit);

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
    public void GIVEN_FullOutputSchemaMode_WHEN_Preflighting_THEN_ShouldValidateRequestAndResponseContracts()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool();

        var result = target.Preflight([tool], ToolOutputSchemaMode.Full);

        result.Succeeded.Should().BeTrue();
        result.Failures.Should().BeNull();
        schemaFactory.Verify(factory => factory.CreateInputSchemaForType(typeof(TestRequest)), Times.Once);
        schemaFactory.Verify(
            factory => factory.CreateOutputSchema(PublishedToolKind.Query, typeof(TestResponse)),
            Times.Once);
    }

    [Fact]
    public void GIVEN_OmittedOutputSchemaMode_WHEN_Preflighting_THEN_ShouldNotValidateResponseContract()
    {
        var schemaFactory = new Mock<IToolSchemaFactory>();
        var target = new PluginTransportSchemaPreflight(schemaFactory.Object);
        var tool = CreatePreparedTool();

        var result = target.Preflight([tool], ToolOutputSchemaMode.Omit);

        result.Succeeded.Should().BeTrue();
        schemaFactory.Verify(
            factory => factory.CreateOutputSchema(It.IsAny<PublishedToolKind>(), It.IsAny<Type>()),
            Times.Never);
    }

    private static PreparedPluginTool CreatePreparedTool()
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
                Kind = ToolKind.Query,
                RequestType = typeof(TestRequest),
                ResponseType = typeof(TestResponse),
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
    private sealed record TestResponse
    {
    }
}
