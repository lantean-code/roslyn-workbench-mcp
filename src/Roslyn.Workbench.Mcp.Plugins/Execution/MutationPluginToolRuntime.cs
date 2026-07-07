using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins.Protocol;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class MutationPluginToolRuntime<TRequest> : IPluginToolRuntime
    where TRequest : WorkspaceBoundRequest
{
    private readonly RegisteredTool _tool;
    private readonly IMutationToolHandler<TRequest> _handler;

    public MutationPluginToolRuntime(
        RegisteredTool tool,
        IMutationToolHandler<TRequest> handler)
    {
        _tool = tool;
        _handler = handler;
    }

    public async ValueTask<CallToolResult> InvokeAsync(
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(contextFactory);

        try
        {
            var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
            var contextLease = contextFactory.CreateMutationContext(request, cancellationToken);
            await using var _ = contextLease.ConfigureAwait(false);

            if (contextLease.ShortCircuitResult is not null)
            {
                return CreateCallToolResult(contextLease.ShortCircuitResult);
            }

            var context = contextLease.Context
                ?? throw new InvalidOperationException("Mutation context acquisition completed without a mutation context.");
            var proposalResult = await _handler.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
            var boxedResult = PluginExecutionResultBox.From(proposalResult);

            if (boxedResult.Outcome != ToolOutcome.Succeeded
                || boxedResult.Data is not MutationProposal proposal)
            {
                return CreateCallToolResult(boxedResult);
            }

            var stagedResult = await context
                .StageAsync(_tool, proposal, boxedResult.Diagnostics, boxedResult.Warnings, cancellationToken)
                .ConfigureAwait(false);

            return CreateCallToolResult(PluginExecutionResultBox.From(stagedResult));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return CreateCallToolResult(PluginExecutionResultBox.CreateUnhandledException());
        }
    }

    private static CallToolResult CreateCallToolResult(PluginExecutionResultBox result)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = PluginToolResultSerializer.Serialize(ToolKind.Mutation, typeof(MutationData), result),
            IsError = result.Outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted,
        };
    }
}
