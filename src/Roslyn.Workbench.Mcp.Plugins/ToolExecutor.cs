using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins;

public sealed class ToolExecutor
{
    private readonly IToolExecutionContextFactory _contextFactory;

    public ToolExecutor(IToolExecutionContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async ValueTask<CallToolResult> ExecuteAsync(
        RegisteredTool tool,
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(arguments);

        var request = DeserializeRequest(tool.RequestType, arguments);
        var contextLease = CreateContext(tool, request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);
        var context = contextLease.Context;

        if (contextLease.ShortCircuitResult is not null)
        {
            var shortCircuitContent = tool.ResponseWriter(contextLease.ShortCircuitResult);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = shortCircuitContent,
                IsError = IsErrorOutcome(contextLease.ShortCircuitResult.Outcome),
            };
        }

        try
        {
            var pluginResult = await tool.Invoker.ExecuteAsync(request, context!, cancellationToken);
            var effectiveResult = await StageMutationProposalAsync(tool, context, pluginResult, cancellationToken);
            var structuredContent = tool.ResponseWriter(effectiveResult);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = structuredContent,
                IsError = IsErrorOutcome(effectiveResult.Outcome),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var fault = PluginExecutionResultBox.CreateUnhandledException();
            var structuredContent = tool.ResponseWriter(fault);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = structuredContent,
                IsError = true,
            };
        }
    }

    private ToolExecutionContextLease<IToolExecutionContext> CreateContext(RegisteredTool tool, WorkspaceBoundRequest request, CancellationToken cancellationToken)
    {
        return tool.Kind switch
        {
            ToolKind.Query => ConvertLease(_contextFactory.CreateQueryContext(request, cancellationToken)),
            ToolKind.Mutation => ConvertLease(_contextFactory.CreateMutationContext(request, cancellationToken)),
            _ => throw new InvalidOperationException($"Unsupported tool kind '{tool.Kind}'."),
        };
    }

    private static ToolExecutionContextLease<IToolExecutionContext> ConvertLease<TContext>(ToolExecutionContextLease<TContext> lease)
        where TContext : class, IToolExecutionContext
    {
        return lease.ShortCircuitResult is null
            ? ToolExecutionContextLease<IToolExecutionContext>.Acquired(lease.Context!, lease)
            : ToolExecutionContextLease<IToolExecutionContext>.Rejected(lease.ShortCircuitResult, lease.Context, lease);
    }

    private static async ValueTask<PluginExecutionResultBox> StageMutationProposalAsync(
        RegisteredTool tool,
        IToolExecutionContext? context,
        PluginExecutionResultBox result,
        CancellationToken cancellationToken)
    {
        if (tool.Kind != ToolKind.Mutation || result.Outcome != ToolOutcome.Succeeded || result.Data is not MutationProposal proposal || context is not IMutationContext mutationContext)
        {
            return result;
        }

        var stagedResult = await mutationContext.StageAsync(tool, proposal, result.Diagnostics, result.Warnings, cancellationToken);
        return PluginExecutionResultBox.From(stagedResult);
    }

    private static WorkspaceBoundRequest DeserializeRequest(Type requestType, IDictionary<string, JsonElement> arguments)
    {
        var request = ToolRequestBinder.Deserialize(requestType, arguments);
        return request as WorkspaceBoundRequest
            ?? throw new InvalidOperationException(
                $"Registered tool request type '{requestType.FullName}' must derive from '{typeof(WorkspaceBoundRequest).FullName}'.");
    }

    private static bool IsErrorOutcome(ToolOutcome outcome)
    {
        return outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted;
    }
}
