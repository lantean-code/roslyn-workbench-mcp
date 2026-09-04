namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginInvocationAnalyzerTests
{
    [Fact]
    public async Task GIVEN_PluginThrowsProtocolException_WHEN_Analyzing_THEN_ShouldReportRwmcp023()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin-id", "Plugin", "1.0")]
            public sealed class PluginEntryPoint
            {
            }

            public static class PluginHelper
            {
                public static void ThrowFailure()
                {
                    {|RWMCP023:throw new ModelContextProtocol.McpProtocolException();|}
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginThrowsProtocolExceptionVariable_WHEN_Analyzing_THEN_ShouldReportRwmcp023()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin-id", "Plugin", "1.0")]
            public sealed class PluginEntryPoint
            {
            }

            public static class PluginHelper
            {
                public static void ThrowFailure(ModelContextProtocol.McpProtocolException exception)
                {
                    {|RWMCP023:throw exception;|}
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginThrowsDerivedProtocolException_WHEN_Analyzing_THEN_ShouldReportRwmcp023()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin-id", "Plugin", "1.0")]
            public sealed class PluginEntryPoint
            {
            }

            public sealed class DerivedProtocolException : ModelContextProtocol.McpProtocolException
            {
            }

            public static class PluginHelper
            {
                public static void ThrowFailure()
                {
                    {|RWMCP023:throw new DerivedProtocolException();|}
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginRethrowsProtocolException_WHEN_Analyzing_THEN_ShouldReportRwmcp023()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin-id", "Plugin", "1.0")]
            public sealed class PluginEntryPoint
            {
            }

            public static class PluginHelper
            {
                public static void ThrowFailure()
                {
                    try
                    {
                    }
                    catch (ModelContextProtocol.McpProtocolException)
                    {
                        {|RWMCP023:throw;|}
                    }
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginThrowsOrdinaryException_WHEN_Analyzing_THEN_ShouldNotReportRwmcp023()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin-id", "Plugin", "1.0")]
            public sealed class PluginEntryPoint
            {
            }

            public static class PluginHelper
            {
                public static void ThrowFailure()
                {
                    throw new System.InvalidOperationException();
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_NonPluginCodeThrowsProtocolException_WHEN_Analyzing_THEN_ShouldNotReportRwmcp023()
    {
        const string source = """
            public static class ApplicationHelper
            {
                public static void ThrowFailure()
                {
                    throw new ModelContextProtocol.McpProtocolException();
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginDoesNotReferenceMcpSdk_WHEN_Analyzing_THEN_ShouldNotReportRwmcp023()
    {
        const string source = """
            [Roslyn.Workbench.Mcp.Plugins.RoslynPlugin("plugin-id", "Plugin", "1.0")]
            public sealed class PluginEntryPoint
            {
            }

            public static class PluginHelper
            {
                public static void ThrowFailure()
                {
                    throw new System.InvalidOperationException();
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationWithoutMcpSdkAsync(source);
    }

    [Fact]
    public async Task GIVEN_HandlerIgnoresCancellation_WHEN_Analyzing_THEN_ShouldReportRwmcp013()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

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
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

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
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

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
    public async Task GIVEN_HandlerExecuteMethodHasNoCancellationToken_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler : Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                public void ExecuteAsync(Request request, object context)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_MutationHandlerIgnoresCancellation_WHEN_Analyzing_THEN_ShouldReportRwmcp013()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceMutationRequest;

            public sealed class Handler : Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler<Request>
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
    public async Task GIVEN_NonExecuteMethodHasCancellationToken_WHEN_Analyzing_THEN_ShouldNotReport()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler : Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                public void Observe(System.Threading.CancellationToken cancellationToken)
                {
                }
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_ParenthesizedDiscardedCancellation_WHEN_Analyzing_THEN_ShouldReportRwmcp013()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;
            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse;

            public sealed class Handler : Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
                public void ExecuteAsync(
                    Request request,
                    object context,
                    System.Threading.CancellationToken {|RWMCP013:cancellationToken|})
                {
                    _ = (object)cancellationToken;
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

            public sealed class RawResponse : System.Collections.Generic.List<string>, Roslyn.Workbench.Mcp.Plugins.IQueryResponse
            {
            }

            public sealed class {|RWMCP014:Handler|} : Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, RawResponse>
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

            public abstract record ResponseBase : Roslyn.Workbench.Mcp.Plugins.IQueryResponse
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

            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse
            {
                public Roslyn.Workbench.Mcp.Workspace.Results.BoundedCollection<string> Items { get; init; } =
                    new Roslyn.Workbench.Mcp.Workspace.Results.BoundedCollection<string>();
            }

            public sealed class Handler :
                Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
            {
            }
            """;

        await AnalyzerVerifier.VerifyInvocationAsync(source);
    }

    [Fact]
    public async Task GIVEN_QueryResponseHasArrayProperty_WHEN_Analyzing_THEN_ShouldReportRwmcp014()
    {
        const string source = """
            public sealed record Request : Roslyn.Workbench.Mcp.Plugins.WorkspaceBoundRequest;

            public sealed record Response : Roslyn.Workbench.Mcp.Plugins.IQueryResponse
            {
                public string[] {|RWMCP014:Items|} { get; init; } = new string[0];
                public System.Collections.Generic.IAsyncEnumerable<string> {|RWMCP014:Stream|} { get; init; }
                public string Name { get; init; } = "Name";
                public object Value { get; init; } = new object();
                private System.Collections.Generic.List<string> Hidden { get; init; }
                    = new System.Collections.Generic.List<string>();
                public static System.Collections.Generic.List<string> Shared { get; }
                    = new System.Collections.Generic.List<string>();
            }

            public sealed class Handler : Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler<Request, Response>
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
