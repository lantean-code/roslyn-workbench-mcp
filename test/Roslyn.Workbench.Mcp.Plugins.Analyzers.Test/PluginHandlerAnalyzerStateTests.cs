namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginHandlerAnalyzerStateTests
{
    [Fact]
    public async Task GIVEN_HandlerHasInstanceField_WHEN_Analyzing_THEN_ShouldReportRwmcp009()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                private int {|RWMCP009:_value|};
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerHasMutableStaticField_WHEN_Analyzing_THEN_ShouldReportRwmcp010()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                private static int {|RWMCP010:_value|};
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerInheritsSourceState_WHEN_Analyzing_THEN_ShouldReportDeclaration()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            public abstract class HandlerBase
            {
                protected int {|RWMCP009:Value|} { get; set; }
            }

            public sealed class Handler :
                HandlerBase,
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }
}
