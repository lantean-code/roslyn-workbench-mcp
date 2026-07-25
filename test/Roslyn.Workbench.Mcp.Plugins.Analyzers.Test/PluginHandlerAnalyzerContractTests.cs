namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginHandlerAnalyzerContractTests
{
    [Fact]
    public async Task GIVEN_MarkerOnlyHandler_WHEN_Analyzing_THEN_ShouldReportRwmcp005()
    {
        const string source = """
            public sealed class {|RWMCP005:Handler|} :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler
            {
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerImplementsBothFamilies_WHEN_Analyzing_THEN_ShouldReportRwmcp005()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceMutationRequest;
            public sealed record Response;

            public sealed class {|RWMCP005:Handler|} :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>,
                Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler<Request>
            {
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_ExternalPluginUsesInternalRequest_WHEN_Analyzing_THEN_ShouldReportRwmcp008()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin", "Plugin", "1")]
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }

            internal sealed record {|RWMCP008:Request|} :
                Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            public sealed record Response;

            internal sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_BundledHandlerUsesInternalRequest_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            internal sealed record Request :
                Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            internal sealed record Response;

            internal sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_QueryDeclaresDestructiveBehaviour_WHEN_Analyzing_THEN_ShouldReportRwmcp012()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            [{|RWMCP012:Roslyn.Workbench.Mcp.Plugins.RoslynTool(
                "query",
                "Query",
                "Queries the workspace.",
                Destructive = true)|}]
            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_ValidExternalQueryHandler_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin", "Plugin", "1")]
            public sealed class Plugin : Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin
            {
                public void Configure(Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration configuration)
                {
                }
            }

            public sealed record Request :
                Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            public sealed record Response;

            [Roslyn.Workbench.Mcp.Plugins.RoslynTool(
                "query",
                "Query",
                "Queries the workspace.")]
            internal sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }
}
