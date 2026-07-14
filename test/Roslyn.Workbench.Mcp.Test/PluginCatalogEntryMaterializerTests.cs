namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginCatalogEntryMaterializerTests
{
    private readonly Mock<IPluginToolRegistrationMaterializer> _toolRegistrationMaterializer;
    private readonly PluginCatalogEntryMaterializer _target;

    public PluginCatalogEntryMaterializerTests()
    {
        _toolRegistrationMaterializer = new Mock<IPluginToolRegistrationMaterializer>();
        _target = new PluginCatalogEntryMaterializer(_toolRegistrationMaterializer.Object);
    }

    [Fact]
    public void GIVEN_MaterializedToolsAndWarnings_WHEN_MaterializingEntry_THEN_ShouldReturnEnabledAtomicResult()
    {
        var plugin = CreatePreparedPlugin();
        var registration = new Mock<IRegisteredPluginTool>();
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
        });

        var result = _target.Materialize(plugin);

        result.Tools.Should().ContainSingle().Which.Should().BeSameAs(registration.Object);
        result.Status.Enabled.Should().BeTrue();
        result.Status.PluginId.Should().Be("plugin");
        result.Status.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginHandlerState"
            && diagnostic.Message == "Warning");
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

    private static PreparedCatalogPlugin CreatePreparedPlugin()
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
                        HandlerFactory = static () => new object(),
                        Tool = new RegisteredTool
                        {
                            Plugin = metadata,
                            Metadata = new ToolRegistrationMetadata
                            {
                                Name = "tool",
                                Title = "Title",
                                Description = "Description",
                            },
                            Kind = ToolKind.Mutation,
                            RequestType = typeof(WorkspaceBoundRequest),
                            ResponseType = typeof(MutationData),
                        },
                    },
                ],
            },
        };
    }
}
