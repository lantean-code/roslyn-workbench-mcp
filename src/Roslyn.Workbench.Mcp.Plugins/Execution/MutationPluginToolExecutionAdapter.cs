using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins.Protocol;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class MutationPluginToolExecutionAdapter<TRequest> : IPluginToolExecutionAdapter
    where TRequest : WorkspaceBoundRequest
{
    private readonly RegisteredTool _tool;
    private readonly IMutationToolHandler<TRequest> _handler;

    public MutationPluginToolExecutionAdapter(
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

            if (proposalResult.Outcome.IsError())
            {
                return CreateCallToolResult(proposalResult);
            }

            if (proposalResult.Outcome == ToolOutcome.NoChange || proposalResult.Data is null)
            {
                return CreateCallToolResult(PluginExecutionResult<MutationData>.NoChange(
                    diagnostics: proposalResult.Diagnostics,
                    warnings: proposalResult.Warnings));
            }

            var stagedResult = await context
                .StageAsync(_tool, proposalResult.Data, proposalResult.Diagnostics, proposalResult.Warnings, cancellationToken)
                .ConfigureAwait(false);

            return CreateCallToolResult(stagedResult);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return CreateCallToolResult(ToolExecutionFailureResult.CreateUnhandledException());
        }
    }

    private static CallToolResult CreateCallToolResult(PluginExecutionResult<MutationProposal> result)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = PluginPublishedResultSerializer.SerializeFailure(new ToolExecutionFailureResult
            {
                Outcome = result.Outcome,
                Error = result.Error ?? throw new InvalidOperationException("Mutation failure result must provide an error."),
                RequiredAction = result.RequiredAction,
                Diagnostics = result.Diagnostics,
                Warnings = result.Warnings,
            }),
            IsError = result.Outcome.IsError(),
        };
    }

    private static CallToolResult CreateCallToolResult(PluginExecutionResult<MutationData> result)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = PluginPublishedResultSerializer.SerializeMutation(result),
            IsError = result.Outcome.IsError(),
        };
    }

    private static CallToolResult CreateCallToolResult(ToolExecutionFailureResult result)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = PluginPublishedResultSerializer.SerializeFailure(result),
            IsError = result.Outcome.IsError(),
        };
    }
}
