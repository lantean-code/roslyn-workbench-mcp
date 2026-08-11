namespace Roslyn.Workbench.Mcp.Plugins.Test.Validation;

public sealed class PluginHandlerContractResolverTests
{
    private readonly PluginHandlerContractResolver _target;

    public PluginHandlerContractResolverTests()
    {
        _target = new PluginHandlerContractResolver();
    }

    [Theory]
    [InlineData(typeof(QueryHandler), false, typeof(IQueryToolHandler<Request, Response>))]
    [InlineData(typeof(MutationHandler), true, typeof(IMutationToolHandler<Request>))]
    [InlineData(typeof(PublicResponseQueryHandler), false, typeof(IQueryToolHandler<Request, string>))]
    [InlineData(typeof(PublicGenericResponseQueryHandler), false, typeof(IQueryToolHandler<Request, IReadOnlyList<Response>>))]
    public void GIVEN_ValidHandlerContract_WHEN_Resolving_THEN_ShouldReturnContract(
        Type handlerType,
        bool isMutation,
        Type expectedContract)
    {
        var kind = isMutation ? ToolKind.Mutation : ToolKind.Query;
        var definition = CreateDefinition(handlerType, kind);

        var result = _target.TryResolve(
            definition,
            PluginContractAccessibility.PublicOnly,
            out var contract,
            out var diagnostic);

        result.Should().BeTrue();
        contract.Should().Be(expectedContract);
        diagnostic.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(MissingContractHandler), "exactly one query")]
    [InlineData(typeof(DualFamilyHandler), "other family")]
    [InlineData(typeof(MultipleQueryContractsHandler), "exactly one query")]
    public void GIVEN_InvalidHandlerContract_WHEN_Resolving_THEN_ShouldRejectHandler(Type handlerType, string message)
    {
        var definition = CreateDefinition(handlerType, ToolKind.Query);

        var result = _target.TryResolve(
            definition,
            PluginContractAccessibility.PublicOnly,
            out var contract,
            out var diagnostic);

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

        var result = _target.TryResolve(
            definition,
            PluginContractAccessibility.PublicOnly,
            out var contract,
            out var diagnostic);

        result.Should().BeFalse();
        contract.Should().BeNull();
        diagnostic.Should().Match<DiagnosticInfo>(value =>
            value.Id == "PluginHandlerContract"
            && value.Message.Contains("must be public", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_NonPublicTransportContractForBundledTool_WHEN_Resolving_THEN_ShouldReturnContract()
    {
        var definition = CreateDefinition(typeof(PrivateContractHandler), ToolKind.Query);

        var result = _target.TryResolve(
            definition,
            PluginContractAccessibility.AllowNonPublic,
            out var contract,
            out var diagnostic);

        result.Should().BeTrue();
        contract.Should().Be<IQueryToolHandler<PrivateRequest, Response>>();
        diagnostic.Should().BeNull();
    }

    private static ConfiguredToolDefinition CreateDefinition(Type handlerType, ToolKind kind)
    {
        IToolConfigurationBuilderState builder;
        if (kind == ToolKind.Query)
        {
            builder = new QueryToolConfigurationBuilder();
        }
        else
        {
            builder = new MutationToolConfigurationBuilder();
        }

        return new ConfiguredToolDefinition
        {
            HandlerType = handlerType,
            Kind = kind,
            Builder = builder,
        };
    }

#pragma warning disable CA1515 // These fixtures must be externally visible to exercise the valid public plugin-contract path.
    public sealed record Request : WorkspaceMutationRequest;

    public sealed record SecondRequest : WorkspaceBoundRequest;

    public sealed record Response;
#pragma warning restore CA1515

#pragma warning disable CA1812 // Contract fixtures are inspected as Type metadata and are not runtime handler instances.
    private sealed record PrivateRequest : WorkspaceBoundRequest;

    private sealed record PrivateResponse;

    private interface IHandlerMarker
    {
    }

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>, IHandlerMarker
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MutationHandler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.NoChange<MutationCandidate>());
        }
    }

    private sealed class PublicResponseQueryHandler : IQueryToolHandler<Request, string>
    {
        public ValueTask<PluginExecutionResult<string>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.Success("Response"));
        }
    }

    private sealed class PublicGenericResponseQueryHandler : IQueryToolHandler<Request, IReadOnlyList<Response>>
    {
        public ValueTask<PluginExecutionResult<IReadOnlyList<Response>>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.Success<IReadOnlyList<Response>>([]));
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
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }

        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
            Request request,
            IMutationContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.NoChange<MutationCandidate>());
        }
    }

    private sealed class MultipleQueryContractsHandler :
        IQueryToolHandler<Request, Response>,
        IQueryToolHandler<SecondRequest, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(SecondRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class PrivateContractHandler : IQueryToolHandler<PrivateRequest, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(PrivateRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class NestedPrivateContractHandler : IQueryToolHandler<Request, IReadOnlyList<PrivateResponse>>
    {
        public ValueTask<PluginExecutionResult<IReadOnlyList<PrivateResponse>>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.Success<IReadOnlyList<PrivateResponse>>([]));
        }
    }

    private sealed class ArrayPrivateContractHandler : IQueryToolHandler<Request, PrivateResponse[]>
    {
        public ValueTask<PluginExecutionResult<PrivateResponse[]>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.Success<PrivateResponse[]>([]));
        }
    }

#pragma warning restore CA1812
}
