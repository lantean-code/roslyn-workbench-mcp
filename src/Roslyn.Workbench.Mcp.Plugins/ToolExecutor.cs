using System.Text.Json;

using Roslyn.Workbench.Mcp.Contracts.Results;

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
        var contextLease = await CreateContextAsync(tool, request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);
        var context = contextLease.Context;

        if (contextLease.ShortCircuitResult is not null)
        {
            var shortCircuitContent = SerializeStructuredResult(tool.ResponseDescriptor, tool.PublishedResponseType, contextLease.ShortCircuitResult);

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
            var structuredContent = SerializeStructuredResult(tool.ResponseDescriptor, tool.PublishedResponseType, effectiveResult);

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
            var structuredContent = SerializeStructuredResult(tool.ResponseDescriptor, tool.PublishedResponseType, fault);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = structuredContent,
                IsError = true,
            };
        }
    }

    private async ValueTask<ToolExecutionContextLease<IToolExecutionContext>> CreateContextAsync(RegisteredTool tool, object request, CancellationToken cancellationToken)
    {
        return tool.Kind switch
        {
            ToolKind.Query => ConvertLease(await _contextFactory.CreateQueryContextAsync(tool, request, cancellationToken)),
            ToolKind.Mutation => ConvertLease(await _contextFactory.CreateMutationContextAsync(tool, request, cancellationToken)),
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

    private static object DeserializeRequest(Type requestType, IDictionary<string, JsonElement> arguments)
    {
        return ToolRequestBinder.Deserialize(requestType, arguments);
    }

    private static JsonElement SerializeStructuredResult(ToolResponseDescriptor descriptor, Type publishedResponseType, PluginExecutionResultBox result)
    {
        return ToolResponseShaper.Shape(descriptor, publishedResponseType, result);
    }

    private static bool IsErrorOutcome(ToolOutcome outcome)
    {
        return outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted;
    }
}
