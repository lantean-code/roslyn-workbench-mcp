namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginEntryPointAnalyzerTests
{
    [Fact]
    public async Task GIVEN_MarkedTypeIsNotPlugin_WHEN_Analyzing_THEN_ShouldReportRwmcp015()
    {
        const string source = """
            [{|RWMCP015:Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin", "Plugin", "1.0")|}]
            public sealed class Plugin
            {
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_ConcretePluginHasNoMarker_WHEN_Analyzing_THEN_ShouldReportRwmcp015()
    {
        const string source = """
            public sealed class {|RWMCP015:Plugin|} :
                Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_AbstractPluginBaseHasNoMarker_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public abstract class PluginBase :
                Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public abstract void Configure(
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration);
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_AssemblyHasMultipleMarkedPlugins_WHEN_Analyzing_THEN_ShouldReportEachMarker()
    {
        const string source = """
            [{|RWMCP016:Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("first", "First", "1.0")|}]
            public sealed class FirstPlugin :
                Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }

            [{|RWMCP016:Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("second", "Second", "1.0")|}]
            public sealed class SecondPlugin :
                Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginDeclaresUnsupportedApiVersion_WHEN_Analyzing_THEN_ShouldReportRwmcp017()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin(
                "plugin",
                "Plugin",
                {|RWMCP017:"2.0"|})]
            public sealed class Plugin :
                Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginIdentityIsBlank_WHEN_Analyzing_THEN_ShouldReportRwmcp018()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin(
                {|RWMCP018:" "|},
                {|RWMCP018:""|},
                "1.0")]
            public sealed class Plugin :
                Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(
                    Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_ToolMetadataDecoratesNonHandler_WHEN_Analyzing_THEN_ShouldReportRwmcp019()
    {
        const string source = """
            [{|RWMCP019:Roslyn.Workbench.Mcp.Plugins.RoslynTool(
                "tool",
                "Tool",
                "Tool description.")|}]
            public sealed class Tool
            {
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_ToolMetadataDecoratesHandler_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            [Roslyn.Workbench.Mcp.Plugins.RoslynTool(
                "tool",
                "Tool",
                "Tool description.")]
            public sealed class Tool :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }

    [Fact]
    public async Task GIVEN_OrdinaryDependencyAssembly_WHEN_Analyzing_THEN_ShouldNotRequireEntryPoint()
    {
        const string source = """
            public static class PluginUtilities
            {
                public static string GetName()
                {
                    return "Name";
                }
            }
            """;

        await AnalyzerVerifier.VerifyEntryPointAsync(source);
    }
}
