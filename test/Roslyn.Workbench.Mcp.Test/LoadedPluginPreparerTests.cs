using System.Reflection;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class LoadedPluginPreparerTests
{
    private readonly Mock<IPluginComposer> _composer;
    private readonly Mock<IPluginConfigurationPreparer> _configurationPreparer;
    private readonly LoadedPluginPreparer _target;

    public LoadedPluginPreparerTests()
    {
        _composer = new Mock<IPluginComposer>();
        _configurationPreparer = new Mock<IPluginConfigurationPreparer>();
        _target = new LoadedPluginPreparer(_composer.Object, _configurationPreparer.Object);
    }

    [Fact]
    public void GIVEN_LoadedPluginAssembly_WHEN_Preparing_THEN_ShouldComposeFreezeAndPrepareConfiguration()
    {
        var assembly = typeof(BundledCorePlugin).Assembly;
        var entryPoint = new PluginEntryPointMetadata
        {
            PluginId = "PluginId",
            DisplayName = "DisplayName",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };
        var preparation = new PluginPreparationResult();
        var configurations = new List<PluginConfiguration>();
        _composer
            .Setup(value => value.Configure(assembly, It.IsAny<IPluginConfiguration>()))
            .Callback<Assembly, IPluginConfiguration>((_, configuration) => configurations.Add((PluginConfiguration)configuration))
            .Returns(PluginCompositionResult.Success());
        _configurationPreparer
            .Setup(value => value.Prepare(It.IsAny<PluginMetadata>(), It.IsAny<PluginConfiguration>()))
            .Returns(preparation);

        var result = _target.Prepare(assembly, entryPoint);

        result.Metadata.PluginId.Should().Be("PluginId");
        result.Metadata.DisplayName.Should().Be("DisplayName");
        result.Metadata.Version.Should().Be("1.0.0");
        result.Metadata.SupportedApiVersion.Should().Be(PluginApiVersions.V1);
        result.Preparation.Should().BeSameAs(preparation);
        _composer.Verify(value => value.Configure(assembly, It.IsAny<PluginConfiguration>()), Times.Once);
        _configurationPreparer.Verify(value => value.Prepare(
            It.Is<PluginMetadata>(metadata => metadata.PluginId == "PluginId"),
            It.IsAny<PluginConfiguration>()), Times.Once);
        var configuration = configurations.Single();
        var action = () => configuration.AddQueryTool<QueryHandler>();
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_CompositionFailure_WHEN_Preparing_THEN_ShouldReturnCategorisedDiagnosticAndFreezeConfiguration()
    {
        var assembly = typeof(BundledCorePlugin).Assembly;
        var entryPoint = new PluginEntryPointMetadata
        {
            PluginId = "PluginId",
            DisplayName = "DisplayName",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };
        var configurations = new List<PluginConfiguration>();
        _composer
            .Setup(value => value.Configure(assembly, It.IsAny<IPluginConfiguration>()))
            .Callback<Assembly, IPluginConfiguration>((_, configuration) => configurations.Add((PluginConfiguration)configuration))
            .Returns(PluginCompositionResult.Failure("Composition failed"));

        var result = _target.Prepare(assembly, entryPoint);

        result.Metadata.PluginId.Should().Be("PluginId");
        result.Preparation.Tools.Should().BeEmpty();
        result.Preparation.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginComposition"
            && diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Message == "Composition failed");
        _configurationPreparer.Verify(
            static value => value.Prepare(It.IsAny<PluginMetadata>(), It.IsAny<PluginConfiguration>()),
            Times.Never);
        var configuration = configurations.Single();
        var action = () => configuration.AddQueryTool<QueryHandler>();
        action.Should().Throw<InvalidOperationException>();
    }

    public sealed record Request : WorkspaceBoundRequest;

    public sealed record Response;

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>
    {
        public QueryHandler()
        {
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }
}
