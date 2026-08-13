namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginHandlerAnalyzerLifetimeTests
{
    [Fact]
    public async Task GIVEN_DisposableHandler_WHEN_Analyzing_THEN_ShouldReportRwmcp006()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class {|RWMCP006:Handler|} :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>,
                System.IDisposable
            {
                public void Dispose()
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerMemberHasImport_WHEN_Analyzing_THEN_ShouldReportRwmcp007()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                [{|RWMCP007:System.Composition.Import|}]
                public object Value { get; } = new object();
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerHasDisposableField_WHEN_Analyzing_THEN_ShouldReportStateAndRwmcp011()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                private readonly System.IO.MemoryStream {|RWMCP009:{|RWMCP011:_stream|}|} = new();
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerHasReadonlyInjectedDisposableService_WHEN_Analyzing_THEN_ShouldNotReportStateOrOwnership()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                private readonly System.IO.MemoryStream _stream;

                public Handler(System.IO.MemoryStream stream)
                {
                    _stream = stream;
                }
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_ConstructorInjectionUsesTransparentExpressions_WHEN_Analyzing_THEN_ShouldNotReportStateOrOwnership()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                private readonly System.IO.MemoryStream _guarded;
                private readonly object _parenthesized;
                private readonly object _cast;
                private readonly object _suppressed;

                public Handler(
                    System.IO.MemoryStream guarded,
                    object parenthesized,
                    object cast,
                    object suppressed)
                {
                    _guarded = guarded ?? throw new System.ArgumentNullException(nameof(guarded));
                    _parenthesized = (parenthesized);
                    _cast = (object)cast;
                    _suppressed = suppressed!;
                }
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_PrimaryConstructorInjectsReadonlyDisposableService_WHEN_Analyzing_THEN_ShouldNotReportStateOrOwnership()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler(System.IO.MemoryStream stream) :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                private readonly System.IO.MemoryStream _stream = stream;
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }

    [Fact]
    public async Task GIVEN_PrimaryConstructorInjectionUsesGuard_WHEN_Analyzing_THEN_ShouldNotReportStateOrOwnership()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler(System.IO.MemoryStream stream) :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                private readonly System.IO.MemoryStream _stream =
                    stream ?? throw new System.ArgumentNullException(nameof(stream));
            }
            """;

        await AnalyzerVerifier.VerifyHandlerAsync(source);
    }
}
