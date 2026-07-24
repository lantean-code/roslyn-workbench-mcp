namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginInvocationAnalyzerTests
{
    [Fact]
    public async Task GIVEN_HandlerIgnoresCancellation_WHEN_Analyzing_THEN_ShouldReportRwmcp013()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                public void ExecuteAsync(
                    Request request,
                    object context,
                    System.Threading.CancellationToken {|RWMCP013:cancellationToken|})
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerDiscardsCancellation_WHEN_Analyzing_THEN_ShouldReportRwmcp013()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                public void ExecuteAsync(
                    Request request,
                    object context,
                    System.Threading.CancellationToken {|RWMCP013:cancellationToken|})
                {
                    _ = cancellationToken;
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerForwardsCancellation_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                public void ExecuteAsync(
                    Request request,
                    object context,
                    System.Threading.CancellationToken cancellationToken)
                {
                    Observe(cancellationToken);
                }

                private static void Observe(
                    System.Threading.CancellationToken cancellationToken)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_QueryReturnsRawCollection_WHEN_Analyzing_THEN_ShouldReportRwmcp014()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            public sealed class {|RWMCP014:Handler|} :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<
                    Request,
                    System.Collections.Generic.IReadOnlyList<string>>
            {
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_QueryResponseHasInheritedRawCollection_WHEN_Analyzing_THEN_ShouldReportDeclaration()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            public abstract record ResponseBase
            {
                public System.Collections.Generic.ISet<string> {|RWMCP014:Items|} { get; init; } =
                    new System.Collections.Generic.HashSet<string>();
            }

            public sealed record Response : ResponseBase;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_QueryUsesBoundedCollection_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            public sealed record Response
            {
                public Roslyn.Workbench.Mcp.Plugins.BoundedCollection<string> Items { get; init; } =
                    new Roslyn.Workbench.Mcp.Plugins.BoundedCollection<string>();
            }

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_IncompleteResponseType_WHEN_Analyzing_THEN_ShouldNotFail()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, {|CS0246:MissingResponse|}>
            {
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }
}
