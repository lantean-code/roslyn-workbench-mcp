namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginHandlerContractResolverTests
{
    private readonly PluginHandlerContractResolver _target;

    public PluginHandlerContractResolverTests()
    {
        _target = new PluginHandlerContractResolver();
    }

    [Theory]
    [InlineData(typeof(QueryHandler), ToolKind.Query, typeof(IQueryToolHandler<Request, Response>))]
    [InlineData(typeof(MutationHandler), ToolKind.Mutation, typeof(IMutationToolHandler<Request>))]
    [InlineData(typeof(PublicResponseQueryHandler), ToolKind.Query, typeof(IQueryToolHandler<Request, string>))]
    [InlineData(typeof(PublicGenericResponseQueryHandler), ToolKind.Query, typeof(IQueryToolHandler<Request, IReadOnlyList<Response>>))]
    public void GIVEN_ValidHandlerContract_WHEN_Resolving_THEN_ShouldReturnContract(
        Type handlerType,
        ToolKind kind,
        Type expectedContract)
    {
        var definition = CreateDefinition(handlerType, kind);

        var result = _target.TryResolve(definition, out var contract, out var diagnostic);

        result.Should().BeTrue();
        contract.Should().Be(expectedContract);
        diagnostic.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(MissingContractHandler), ToolKind.Query, "exactly one query")]
    [InlineData(typeof(DualFamilyHandler), ToolKind.Query, "other family")]
    [InlineData(typeof(MultipleQueryContractsHandler), ToolKind.Query, "exactly one query")]
    public void GIVEN_InvalidHandlerContract_WHEN_Resolving_THEN_ShouldRejectHandler(Type handlerType, ToolKind kind, string message)
    {
        var definition = CreateDefinition(handlerType, kind);

        var result = _target.TryResolve(definition, out var contract, out var diagnostic);

        result.Should().BeFalse();
        contract.Should().BeNull();
        diagnostic.Should().Match<DiagnosticInfo>(value =>
            value.Id == "PluginHandlerContract"
            && value.Severity == DiagnosticSeverity.Error
            && value.Message.Contains(message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(PrivateContractHandler))]
    [InlineData(typeof(NestedPrivateContractHandler))]
    [InlineData(typeof(ArrayPrivateContractHandler))]
    public void GIVEN_NonPublicTransportContract_WHEN_Resolving_THEN_ShouldRejectHandler(Type handlerType)
    {
        var definition = CreateDefinition(handlerType, ToolKind.Query);

        var result = _target.TryResolve(definition, out var contract, out var diagnostic);

        result.Should().BeFalse();
        contract.Should().BeNull();
        diagnostic.Should().Match<DiagnosticInfo>(value =>
            value.Id == "PluginHandlerContract"
            && value.Message.Contains("must be public", StringComparison.Ordinal));
    }

    private static ConfiguredToolDefinition CreateDefinition(Type handlerType, ToolKind kind)
    {
        return new ConfiguredToolDefinition
        {
            HandlerType = handlerType,
            HandlerFactory = static () => new object(),
            Kind = kind,
            Builder = kind == ToolKind.Query
                ? new QueryToolConfigurationBuilder()
                : new MutationToolConfigurationBuilder(),
        };
    }

    public sealed record Request : WorkspaceBoundRequest;

    public sealed record SecondRequest : WorkspaceBoundRequest;

    public sealed record Response;

    private sealed record PrivateRequest : WorkspaceBoundRequest;

    private sealed record PrivateResponse;

    private interface IHandlerMarker
    {
    }

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>, IHandlerMarker
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    private sealed class MutationHandler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<MutationCandidate>.NoChange());
        }
    }

    private sealed class PublicResponseQueryHandler : IQueryToolHandler<Request, string>
    {
        public ValueTask<PluginExecutionResult<string>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<string>.Success("Response"));
        }
    }

    private sealed class PublicGenericResponseQueryHandler : IQueryToolHandler<Request, IReadOnlyList<Response>>
    {
        public ValueTask<PluginExecutionResult<IReadOnlyList<Response>>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<IReadOnlyList<Response>>.Success([]));
        }
    }

    private sealed class MissingContractHandler : IQueryToolHandler
    {
    }

    private sealed class DualFamilyHandler :
        IQueryToolHandler<Request, Response>,
        IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }

        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
            Request request,
            IMutationContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<MutationCandidate>.NoChange());
        }
    }

    private sealed class MultipleQueryContractsHandler :
        IQueryToolHandler<Request, Response>,
        IQueryToolHandler<SecondRequest, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(SecondRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    private sealed class PrivateContractHandler : IQueryToolHandler<PrivateRequest, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(PrivateRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    private sealed class NestedPrivateContractHandler : IQueryToolHandler<Request, IReadOnlyList<PrivateResponse>>
    {
        public ValueTask<PluginExecutionResult<IReadOnlyList<PrivateResponse>>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<IReadOnlyList<PrivateResponse>>.Success([]));
        }
    }

    private sealed class ArrayPrivateContractHandler : IQueryToolHandler<Request, PrivateResponse[]>
    {
        public ValueTask<PluginExecutionResult<PrivateResponse[]>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<PrivateResponse[]>.Success([]));
        }
    }
}
