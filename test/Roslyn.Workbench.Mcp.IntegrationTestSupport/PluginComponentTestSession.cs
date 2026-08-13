namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal sealed class PluginComponentTestSession
{
    private readonly ComponentWorkspace _workspace;
    private readonly PluginToolCatalogue _catalogue;

    public PluginComponentTestSession(ComponentWorkspace workspace, PluginToolCatalogue catalogue)
    {
        _workspace = workspace;
        _catalogue = catalogue;
    }

    public ValueTask<PluginExecutionResult<TResponse>> ExecuteQueryAsync<TRequest, TResponse>(
        string toolName,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : WorkspaceBoundRequest
        where TResponse : IQueryResponse
    {
        var tool = GetTool(toolName);
        return tool.Accept(new QueryVisitor<TRequest, TResponse>(this, request, cancellationToken));
    }

    public ValueTask<PluginExecutionResult<MutationData>> ExecuteMutationAsync<TRequest>(
        string toolName,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : WorkspaceMutationRequest
    {
        var tool = GetTool(toolName);
        return tool.Accept(new MutationVisitor<TRequest>(this, request, cancellationToken));
    }

    private IRegisteredPluginTool GetTool(string toolName)
    {
        return _catalogue.Tools.Single(tool =>
            string.Equals(tool.Tool.Metadata.Name, toolName, StringComparison.Ordinal));
    }

    private async ValueTask<PluginExecutionResult<TResponse>> ExecuteQueryCoreAsync<TRequest, TResponse>(
        PluginQueryRegistration<TRequest, TResponse> registration,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : WorkspaceBoundRequest
        where TResponse : IQueryResponse
    {
        await using var lease = _workspace.PluginContextFactory.CreateQueryContext(request, cancellationToken);
        if (lease.HasShortCircuitResult)
        {
            return lease.ShortCircuitResult.ToPluginExecutionResult<TResponse>();
        }

        return await registration.Handler.ExecuteAsync(request, lease.Context, cancellationToken);
    }

    private async ValueTask<PluginExecutionResult<MutationData>> ExecuteMutationCoreAsync<TRequest>(
        PluginMutationRegistration<TRequest> registration,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : WorkspaceMutationRequest
    {
        await using var lease = _workspace.PluginContextFactory.CreateMutationContext(request, cancellationToken);
        if (lease.HasFailure)
        {
            return lease.Failure.ToPluginExecutionResult<MutationData>();
        }

        var proposal = await registration.Handler.ExecuteAsync(request, lease.Context, cancellationToken);
        if (proposal.Outcome == PluginExecutionOutcome.Succeeded && proposal.Data is not null)
        {
            return await lease.StageAsync(
                registration.Tool.Metadata.Name,
                proposal.Data,
                proposal.Diagnostics,
                proposal.Warnings,
                cancellationToken);
        }

        return proposal.Outcome switch
        {
            PluginExecutionOutcome.NoChange => PluginExecutionResult.NoChange<MutationData>(
                diagnostics: proposal.Diagnostics,
                warnings: proposal.Warnings),
            PluginExecutionOutcome.Rejected when proposal.HasError => PluginExecutionResult.Rejected<MutationData>(
                proposal.Error,
                proposal.RequiredAction,
                proposal.Diagnostics,
                proposal.Warnings),
            PluginExecutionOutcome.Conflict when proposal.HasError => PluginExecutionResult.Conflict<MutationData>(
                proposal.Error,
                proposal.RequiredAction,
                proposal.Diagnostics,
                proposal.Warnings),
            PluginExecutionOutcome.Faulted when proposal.HasError => PluginExecutionResult.Faulted<MutationData>(
                proposal.Error,
                proposal.RequiredAction,
                proposal.Diagnostics,
                proposal.Warnings),
            _ => throw new InvalidOperationException(
                $"Plugin mutation '{registration.Tool.Metadata.Name}' returned an invalid successful result."),
        };
    }

    private sealed class QueryVisitor<TExpectedRequest, TExpectedResponse>
        : IPluginToolRegistrationVisitor<ValueTask<PluginExecutionResult<TExpectedResponse>>>
        where TExpectedRequest : WorkspaceBoundRequest
        where TExpectedResponse : IQueryResponse
    {
        private readonly PluginComponentTestSession _session;
        private readonly TExpectedRequest _request;
        private readonly CancellationToken _cancellationToken;

        public QueryVisitor(
            PluginComponentTestSession session,
            TExpectedRequest request,
            CancellationToken cancellationToken)
        {
            _session = session;
            _request = request;
            _cancellationToken = cancellationToken;
        }

        public ValueTask<PluginExecutionResult<TExpectedResponse>> VisitQuery<TRequest, TResponse>(
            PluginQueryRegistration<TRequest, TResponse> registration)
            where TRequest : WorkspaceBoundRequest
            where TResponse : IQueryResponse
        {
            if (registration is not PluginQueryRegistration<TExpectedRequest, TExpectedResponse> typedRegistration)
            {
                throw new InvalidOperationException(
                    $"Tool '{registration.Tool.Metadata.Name}' does not match the requested component contract.");
            }

            return _session.ExecuteQueryCoreAsync(typedRegistration, _request, _cancellationToken);
        }

        public ValueTask<PluginExecutionResult<TExpectedResponse>> VisitMutation<TRequest>(
            PluginMutationRegistration<TRequest> registration)
            where TRequest : WorkspaceMutationRequest
        {
            throw new InvalidOperationException($"Tool '{registration.Tool.Metadata.Name}' is not a query tool.");
        }
    }

    private sealed class MutationVisitor<TExpectedRequest>
        : IPluginToolRegistrationVisitor<ValueTask<PluginExecutionResult<MutationData>>>
        where TExpectedRequest : WorkspaceMutationRequest
    {
        private readonly PluginComponentTestSession _session;
        private readonly TExpectedRequest _request;
        private readonly CancellationToken _cancellationToken;

        public MutationVisitor(
            PluginComponentTestSession session,
            TExpectedRequest request,
            CancellationToken cancellationToken)
        {
            _session = session;
            _request = request;
            _cancellationToken = cancellationToken;
        }

        public ValueTask<PluginExecutionResult<MutationData>> VisitQuery<TRequest, TResponse>(
            PluginQueryRegistration<TRequest, TResponse> registration)
            where TRequest : WorkspaceBoundRequest
            where TResponse : IQueryResponse
        {
            throw new InvalidOperationException($"Tool '{registration.Tool.Metadata.Name}' is not a mutation tool.");
        }

        public ValueTask<PluginExecutionResult<MutationData>> VisitMutation<TRequest>(
            PluginMutationRegistration<TRequest> registration)
            where TRequest : WorkspaceMutationRequest
        {
            if (registration is not PluginMutationRegistration<TExpectedRequest> typedRegistration)
            {
                throw new InvalidOperationException(
                    $"Tool '{registration.Tool.Metadata.Name}' does not match the requested component contract.");
            }

            return _session.ExecuteMutationCoreAsync(typedRegistration, _request, _cancellationToken);
        }
    }
}
