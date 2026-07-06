using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class MutationToolInvoker<TRequest, TResponse> : IPluginToolInvoker
    where TRequest : WorkspaceBoundRequest
{
    private readonly IMutationToolHandler<TRequest, TResponse> _handler;

    public MutationToolInvoker(IMutationToolHandler<TRequest, TResponse> handler)
    {
        _handler = handler;
    }

    public async ValueTask<PluginExecutionResultBox> ExecuteAsync(
        RegisteredTool tool,
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        var contextLease = contextFactory.CreateMutationContext(request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);

        if (contextLease.ShortCircuitResult is not null)
        {
            return contextLease.ShortCircuitResult;
        }

        var context = contextLease.Context
            ?? throw new InvalidOperationException("Mutation context acquisition completed without a mutation context.");
        var proposalResult = await _handler
            .ExecuteAsync(request, context, cancellationToken)
            .ConfigureAwait(false);
        var boxedResult = PluginExecutionResultBox.From(proposalResult);

        if (boxedResult.Outcome != ToolOutcome.Succeeded
            || boxedResult.Data is not MutationProposal proposal)
        {
            return boxedResult;
        }

        var stagedResult = await context
            .StageAsync(tool, proposal, boxedResult.Diagnostics, boxedResult.Warnings, cancellationToken)
            .ConfigureAwait(false);

        return PluginExecutionResultBox.From(stagedResult);
    }
}
