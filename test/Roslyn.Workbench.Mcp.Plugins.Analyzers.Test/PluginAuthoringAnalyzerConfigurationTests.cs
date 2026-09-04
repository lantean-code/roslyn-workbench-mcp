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

    [Fact]
    public async Task GIVEN_ConfigurationAssignedToLocal_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration local = null;
                    local = configuration;
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_NullAssignedToField_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                private object _value;

                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    _value = null;
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_ImmediatelyInvokedDelegateCapturesConfiguration_WHEN_Analyzing_THEN_ShouldNotTreatItAsEscaping()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    new System.Action(() => configuration.AddQueryTool<Plugin>())();
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_UnrelatedAsyncMethod_WHEN_Analyzing_THEN_ShouldNotReportConfigurationDiagnostic()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }

                public async void RunAsync()
                {
                    await System.Threading.Tasks.Task.Yield();
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_OrdinaryValueAssignedToField_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                private object _value;

                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    _value = new object();
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_LocalBuilderCapturedByReturnedDelegate_WHEN_Analyzing_THEN_ShouldReportRwmcp004()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }

                public System.Action Create(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    var builder = configuration.AddQueryTool<Plugin>();
                    return {|RWMCP004:() => Consume(builder)|};
                }

                private static void Consume(object value)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_EscapingDelegateDoesNotCaptureConfiguration_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                private System.Action _action;

                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    _action = () => Consume("Value");
                }

                private static void Consume(string value)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_DelegateParameterUsesConfigurationType_WHEN_Analyzing_THEN_ShouldNotTreatItAsCaptured()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                private System.Action<Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration> _action;

                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    _action = value => value.AddQueryTool<Plugin>();
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task GIVEN_FluentToolNameViolatesLengthRules_WHEN_Analyzing_THEN_ShouldReportRwmcp022(string toolName)
    {
        var source = $$"""
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    configuration.AddQueryTool<Plugin>().WithName({|RWMCP022:"{{toolName}}"|});
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_NullFluentToolName_WHEN_Analyzing_THEN_ShouldDeferValidationToRuntime()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    configuration.AddQueryTool<Plugin>().WithName(null);
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_ToolNameUsesEverySupportedCharacterClass_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                    configuration.AddQueryTool<Plugin>().WithName("AZaz09_-.name");
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }
}
