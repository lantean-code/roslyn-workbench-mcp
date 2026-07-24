namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginAuthoringAnalyzerWorkspaceTests
{
    [Fact]
    public async Task GIVEN_MarkedPluginHelper_WHEN_ApplyingWorkspaceChanges_THEN_ShouldReportRwmcp001()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin", "Plugin", "1")]
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }

            public static class Helper
            {
                public static void Apply(
                    Microsoft.CodeAnalysis.Workspace workspace,
                    Microsoft.CodeAnalysis.Solution solution)
                {
                    {|RWMCP001:workspace.TryApplyChanges(solution)|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerWithoutPluginEntryPoint_WHEN_ApplyingWorkspaceChanges_THEN_ShouldReportRwmcp001()
    {
        const string source = """
            public sealed class Handler : Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler
            {
                public void Apply(
                    Microsoft.CodeAnalysis.Workspace workspace,
                    Microsoft.CodeAnalysis.Solution solution)
                {
                    {|RWMCP001:workspace.TryApplyChanges(solution)|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_OrdinaryAssembly_WHEN_ApplyingWorkspaceChanges_THEN_ShouldNotReport()
    {
        const string source = """
            public static class Helper
            {
                public static void Apply(
                    Microsoft.CodeAnalysis.Workspace workspace,
                    Microsoft.CodeAnalysis.Solution solution)
                {
                    workspace.TryApplyChanges(solution);
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_Rwmcp001Suppressed_WHEN_ApplyingWorkspaceChanges_THEN_ShouldNotReport()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin", "Plugin", "1")]
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }

                public void Apply(
                    Microsoft.CodeAnalysis.Workspace workspace,
                    Microsoft.CodeAnalysis.Solution solution)
                {
            #pragma warning disable RWMCP001
                    workspace.TryApplyChanges(solution);
            #pragma warning restore RWMCP001
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_MarkedPluginHelper_WHEN_ReadingLiveWorkspaceSolution_THEN_ShouldReportRwmcp002()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin", "Plugin", "1")]
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }

            public static class Helper
            {
                public static Microsoft.CodeAnalysis.Solution Read(Microsoft.CodeAnalysis.Workspace workspace)
                {
                    return {|RWMCP002:workspace.CurrentSolution|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerSnapshot_WHEN_ReadingContextSolution_THEN_ShouldNotReport()
    {
        const string source = """
            public interface IContext
            {
                Microsoft.CodeAnalysis.Solution CurrentSolution { get; }
            }

            public sealed class Handler : Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler
            {
                public Microsoft.CodeAnalysis.Solution Read(IContext context)
                {
                    return context.CurrentSolution;
                }
            }
            """;

        await AnalyzerVerifier.VerifyAsync(source);
    }
}
