using System.Composition;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Validation;

public sealed class PluginHandlerTypeInspectorTests
{
    private readonly PluginHandlerTypeInspector _target;

    public PluginHandlerTypeInspectorTests()
    {
        _target = new PluginHandlerTypeInspector();
    }

    [Fact]
    public void GIVEN_ValidHandlerType_WHEN_Inspecting_THEN_ShouldAcceptHandler()
    {
        var result = _target.Inspect(typeof(QueryHandler));

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(DisposableQueryHandler), "disposable")]
    [InlineData(typeof(AsyncDisposableQueryHandler), "disposable")]
    [InlineData(typeof(ImportedQueryHandler), "MEF imports")]
    [InlineData(typeof(ImportingConstructorQueryHandler), "MEF imports")]
    [InlineData(typeof(InheritedImportQueryHandler), "MEF imports")]
    public void GIVEN_InvalidHandlerType_WHEN_Inspecting_THEN_ShouldRejectHandler(Type handlerType, string message)
    {
        var result = _target.Inspect(handlerType);

        result.Should().ContainSingle(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Message.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_HandlerHasIndependentLifetimeAndCompositionIssues_WHEN_Inspecting_THEN_ShouldReportBothErrors()
    {
        var result = _target.Inspect(typeof(DisposableImportedQueryHandler));

        result.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginHandlerLifetime",
            "PluginHandlerComposition");
    }

#pragma warning disable CA1812 // Contract and handler fixtures are inspected through composition metadata without activation.
    private sealed record Request : WorkspaceBoundRequest;

    private sealed record Response;

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    private sealed class DisposableQueryHandler : IQueryToolHandler<Request, Response>, IDisposable
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }

        public void Dispose()
        {
        }
    }

    private sealed class AsyncDisposableQueryHandler : IQueryToolHandler<Request, Response>, IAsyncDisposable
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ImportedQueryHandler : IQueryToolHandler<Request, Response>
    {
        [Import]
        public object? Dependency { get; set; }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    private sealed class DisposableImportedQueryHandler : IQueryToolHandler<Request, Response>, IDisposable
    {
        [Import]
        public object? Dependency { get; set; }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }

        public void Dispose()
        {
        }
    }

    private sealed class ImportingConstructorQueryHandler : IQueryToolHandler<Request, Response>
    {
        [ImportingConstructor]
        public ImportingConstructorQueryHandler()
        {
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    private abstract class ImportedQueryHandlerBase
    {
        [Import]
        private object? Dependency { get; set; }
    }

    private sealed class InheritedImportQueryHandler : ImportedQueryHandlerBase, IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }
#pragma warning restore CA1812
}
