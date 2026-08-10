namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginAuthoringAnalyzerConfigurationTests
{
    [Fact]
    public async Task GIVEN_AsyncPluginConfigure_WHEN_Analyzing_THEN_ShouldReportRwmcp003()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public async void {|RWMCP003:Configure|}(
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_SynchronousPluginConfigure_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_ConfigurationAssignedToField_WHEN_Analyzing_THEN_ShouldReportRwmcp004()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                private object _configuration;

                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    {|RWMCP004:_configuration = configuration|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_BuilderAssignedToProperty_WHEN_Analyzing_THEN_ShouldReportRwmcp004()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                private Roslyn.Workbench.Mcp.Plugins.QueryToolConfigurationBuilder Builder { get; set; }

                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    {|RWMCP004:Builder = configuration.AddQueryTool<Plugin>()|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_ConfigurationCapturedByEscapingDelegate_WHEN_Analyzing_THEN_ShouldReportRwmcp004()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                private System.Action _configure;

                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    _configure = {|RWMCP004:() => configuration.AddQueryTool<Plugin>()|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_ConfigurationUsedByLocalAndSynchronousHelper_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    var builder = configuration.AddQueryTool<Plugin>();
                    Apply(builder);
                }

                private static void Apply(
                    Roslyn.Workbench.Mcp.Plugins.QueryToolConfigurationBuilder builder)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_ConstantFluentToolNameIsInvalid_WHEN_Analyzing_THEN_ShouldReportRwmcp022()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    configuration.AddQueryTool<Plugin>().WithName({|RWMCP022:"invalid/name"|});
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_DynamicFluentToolName_WHEN_Analyzing_THEN_ShouldDeferValidationToRuntime()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    configuration.AddQueryTool<Plugin>().WithName(GetName());
                }

                private static string GetName()
                {
                    return "invalid name";
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }
}
