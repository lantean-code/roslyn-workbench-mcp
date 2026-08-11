namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class MefPluginComposerIntegrationTests
{
    private readonly MefPluginComposer _target = new();

    [Fact]
    public void GIVEN_AssemblyWithOnePluginExport_WHEN_Composing_THEN_ShouldConfigureEntryPoint()
    {
        var configuration = new PluginConfiguration();

        var result = _target.Configure(typeof(BundledCorePlugin).Assembly, configuration);

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        configuration.Definitions.Should().HaveCount(39);
        configuration.ServiceDefinitions.Should().HaveCount(3);
    }

    [Fact]
    public void GIVEN_AssemblyWithoutPluginExport_WHEN_Composing_THEN_ShouldRejectAssembly()
    {
        var configuration = new PluginConfiguration();

        var result = _target.Configure(typeof(MefPluginComposer).Assembly, configuration);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("does not compose");
        configuration.Definitions.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_AssemblyWithMultiplePluginExports_WHEN_Composing_THEN_ShouldRejectAssembly()
    {
        var configuration = new PluginConfiguration();

        var result = _target.Configure(typeof(ValidQueryTestPlugin).Assembly, configuration);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("multiple");
        configuration.Definitions.Should().BeEmpty();
    }
}
