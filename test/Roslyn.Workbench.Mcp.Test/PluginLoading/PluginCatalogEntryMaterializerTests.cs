using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogEntryMaterializerTests
{
    private readonly Mock<IPluginToolRegistrationMaterializer> _toolRegistrationMaterializer;
    private readonly Mock<IPluginTransportSchemaPreflight> _schemaPreflight;
    private readonly Mock<IToolSchemaFactory> _schemaFactory;
    private readonly Mock<ILogger<PluginCatalogEntryMaterializer>> _logger;
    private readonly PluginCatalogEntryMaterializer _target;

    public PluginCatalogEntryMaterializerTests()
    {
        _toolRegistrationMaterializer = new Mock<IPluginToolRegistrationMaterializer>();
        _schemaPreflight = new Mock<IPluginTransportSchemaPreflight>();
        _schemaFactory = new Mock<IToolSchemaFactory>();
        _logger = new Mock<ILogger<PluginCatalogEntryMaterializer>>();
        _schemaPreflight
            .Setup(preflight => preflight.Preflight(It.IsAny<IReadOnlyList<PreparedPluginTool>>()))
            .Returns(PluginTransportSchemaPreflightResult.Success());
        _schemaFactory
            .Setup(factory => factory.CreateInputSchemaForType(It.IsAny<Type>()))
            .Returns(JsonSerializer.SerializeToElement(new { }));

        _target = new PluginCatalogEntryMaterializer(
            _toolRegistrationMaterializer.Object,
            _schemaPreflight.Object,
            _schemaFactory.Object,
            _logger.Object);
    }

    [Fact]
    public void GIVEN_OversizedInputSchema_WHEN_MaterializingEntry_THEN_ShouldLogOnceWithoutAddingStatusDiagnostic()
    {
        var plugin = CreatePreparedPlugin();
        var registration = new Mock<IRegisteredPluginTool>();
        registration.SetupGet(static value => value.Tool).Returns(plugin.Preparation.Tools.Single().Tool);
        _toolRegistrationMaterializer.Setup(value => value.Materialize(plugin.Preparation)).Returns(new PluginMaterializationResult
        {
            Tools = [registration.Object],
        });

        var oversizedDescription = new string('x', InputSchemaBudget.MaximumSizeInBytes);
        var oversizedSchema = JsonSerializer.SerializeToElement(new { description = oversizedDescription });
        _schemaFactory
            .Setup(factory => factory.CreateInputSchemaForType(plugin.Preparation.Tools.Single().Tool.RequestType))
            .Returns(oversizedSchema);
        _logger.Setup(item => item.IsEnabled(LogLevel.Warning)).Returns(true);

        var result = _target.Materialize(plugin);

        var expectedSize = InputSchemaBudget.GetSizeInBytes(oversizedSchema);
        var requestTypeName = plugin.Preparation.Tools.Single().Tool.RequestType.Name;
        result.Status.Enabled.Should().BeTrue();
        result.Status.Diagnostics.Should().BeEmpty();
        _logger.Verify(
            item => item.Log(
                LogLevel.Warning,
                new EventId(2),
                It.Is<It.IsAnyType>((value, _) =>
                    value.ToString() == $"Plugin authoring warning InputSchemaSize for plugin plugin, tool tool: Request '{requestTypeName}' publishes a {expectedSize}-byte input schema. Keep agent-facing input schemas at or below 5,000 bytes so property guidance remains portable across MCP clients."
                    && HasLogProperty(value, "RuleId", "InputSchemaSize")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GIVEN_QueryResponseAuthoringWarning_WHEN_MaterializingEntry_THEN_ShouldLogOnceWithoutAddingStatusDiagnostic()
    {
        var plugin = CreatePreparedPlugin(ToolKind.Query, typeof(RawQueryResponse));
        var registration = new Mock<IRegisteredPluginTool>();
        registration.SetupGet(static value => value.Tool).Returns(plugin.Preparation.Tools.Single().Tool);
        _toolRegistrationMaterializer.Setup(value => value.Materialize(plugin.Preparation)).Returns(new PluginMaterializationResult
        {
            Tools = [registration.Object],
        });

        _logger.Setup(item => item.IsEnabled(LogLevel.Warning)).Returns(true);

        var result = _target.Materialize(plugin);

        result.Status.Enabled.Should().BeTrue();
        result.Status.Diagnostics.Should().BeEmpty();
        _logger.Verify(
            item => item.Log(
                LogLevel.Warning,
                new EventId(1),
                It.Is<It.IsAnyType>((value, _) =>
                    value.ToString() == "Plugin authoring warning RWMCP014 for plugin plugin, tool tool: Response 'RawQueryResponse' publishes unbounded top-level collections: Items. Prefer BoundedCollection<TItem> for agent-facing top-level collections."
                    && HasLogProperty(value, "RuleId", "RWMCP014")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GIVEN_MaterializedToolsAndWarnings_WHEN_MaterializingEntry_THEN_ShouldReturnEnabledAtomicResult()
    {
        var plugin = CreatePreparedPlugin();
        var registration = new Mock<IRegisteredPluginTool>();
        var serviceProviderLifetime = new Mock<IDisposable>();
        registration.SetupGet(static value => value.Tool).Returns(plugin.Preparation.Tools.Single().Tool);
        _toolRegistrationMaterializer.Setup(value => value.Materialize(plugin.Preparation)).Returns(new PluginMaterializationResult
        {
            Tools = [registration.Object],
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "PluginHandlerState",
                    Severity = DiagnosticSeverity.Warning,
                    Message = "Warning",
                },
            ],
            ServiceProviderLifetime = serviceProviderLifetime.Object,
        });

        var result = _target.Materialize(plugin);

        result.Tools.Should().ContainSingle().Which.Should().BeSameAs(registration.Object);
        result.Status.Enabled.Should().BeTrue();
        result.Status.PluginId.Should().Be("plugin");
        result.Status.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginHandlerState"
            && diagnostic.Message == "Warning");
        result.ServiceProviderLifetime.Should().BeSameAs(serviceProviderLifetime.Object);
    }

    [Fact]
    public void GIVEN_PostMaterializationInspectionFails_WHEN_MaterializingEntry_THEN_ShouldDisposePluginServices()
    {
        var plugin = CreatePreparedPlugin();
        var registration = new Mock<IRegisteredPluginTool>();
        var serviceProviderLifetime = new Mock<IDisposable>();
        registration
            .SetupGet(static value => value.Tool)
            .Throws(new InvalidOperationException("Inspection failed."));

        _toolRegistrationMaterializer.Setup(value => value.Materialize(plugin.Preparation)).Returns(new PluginMaterializationResult
        {
            Tools = [registration.Object],
            ServiceProviderLifetime = serviceProviderLifetime.Object,
        });

        var result = _target.Materialize(plugin);

        result.Status.Enabled.Should().BeFalse();
        result.ServiceProviderLifetime.Should().BeNull();
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_InspectionAndCleanupFail_WHEN_MaterializingEntry_THEN_ShouldDisableOnlyThatPlugin()
    {
        var plugin = CreatePreparedPlugin();
        var registration = new Mock<IRegisteredPluginTool>();
        var serviceProviderLifetime = new Mock<IDisposable>();
        registration
            .SetupGet(static value => value.Tool)
            .Throws(new InvalidOperationException("Inspection failed."));
        serviceProviderLifetime
            .Setup(item => item.Dispose())
            .Throws(new IOException("Cleanup failed."));

        _toolRegistrationMaterializer.Setup(value => value.Materialize(plugin.Preparation)).Returns(new PluginMaterializationResult
        {
            Tools = [registration.Object],
            ServiceProviderLifetime = serviceProviderLifetime.Object,
        });

        var result = _target.Materialize(plugin);

        result.Status.Enabled.Should().BeFalse();
        result.Status.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == PluginDiagnosticIds.Materialization
            && diagnostic.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
            && diagnostic.Message.Contains(nameof(IOException), StringComparison.Ordinal));
        result.ServiceProviderLifetime.Should().BeNull();
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_HandlerConstructionFails_WHEN_MaterializingEntry_THEN_ShouldReturnDisabledResultWithoutTools()
    {
        var plugin = CreatePreparedPlugin();
        _toolRegistrationMaterializer.Setup(value => value.Materialize(plugin.Preparation))
            .Throws(new InvalidOperationException("Construction failed"));

        var result = _target.Materialize(plugin);

        result.Tools.Should().BeEmpty();
        result.Status.Enabled.Should().BeFalse();
        result.Status.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginMaterialization"
            && diagnostic.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
            && !diagnostic.Message.Contains("Construction failed", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_TransportSchemaCannotBePublished_WHEN_MaterializingEntry_THEN_ShouldDisablePluginBeforeConstructingHandlers()
    {
        var plugin = CreatePreparedPlugin();
        var diagnostic = new DiagnosticInfo
        {
            Id = PluginDiagnosticIds.ToolSchema,
            Severity = DiagnosticSeverity.Error,
            Message = "Schema failed.",
        };
        _schemaPreflight
            .Setup(preflight => preflight.Preflight(plugin.Preparation.Tools))
            .Returns(PluginTransportSchemaPreflightResult.Failure([diagnostic]));

        var result = _target.Materialize(plugin);

        result.Tools.Should().BeEmpty();
        result.Status.Enabled.Should().BeFalse();
        result.Status.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(diagnostic);
        _toolRegistrationMaterializer.Verify(
            materializer => materializer.Materialize(It.IsAny<PluginPreparationResult>()),
            Times.Never);
    }

    private static bool HasLogProperty(object value, string propertyName, object expectedValue)
    {
        if (value is not IReadOnlyList<KeyValuePair<string, object?>> properties)
        {
            return false;
        }

        return properties.Any(property =>
            string.Equals(property.Key, propertyName, StringComparison.Ordinal)
            && Equals(property.Value, expectedValue));
    }

    private static PreparedCatalogPlugin CreatePreparedPlugin(
        ToolKind kind = ToolKind.Mutation,
        Type? responseType = null)
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin",
            DisplayName = "DisplayName",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        return new PreparedCatalogPlugin
        {
            Metadata = metadata,
            Preparation = new PluginPreparationResult
            {
                Tools =
                [
                    new PreparedPluginTool
                    {
                        HandlerType = typeof(object),
                        HandlerContract = typeof(object),
                        Tool = new RegisteredTool
                        {
                            Plugin = metadata,
                            Metadata = new ToolRegistrationMetadata
                            {
                                Name = "tool",
                                Title = "Title",
                                Description = "Description",
                            },
                            Kind = kind,
                            RequestType = typeof(WorkspaceBoundRequest),
                            ResponseType = responseType ?? typeof(MutationData),
                        },
                    },
                ],
            },
        };
    }

#pragma warning disable CA1812 // Response fixture is inspected through reflection metadata without construction.
    private sealed record RawQueryResponse
    {
        public IReadOnlyList<string> Items { get; init; } = [];
    }
#pragma warning restore CA1812
}
