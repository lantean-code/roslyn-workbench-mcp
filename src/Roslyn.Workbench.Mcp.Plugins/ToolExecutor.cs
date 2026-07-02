using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins;

public sealed class ToolExecutor
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

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
            var shortCircuitContent = SerializeStructuredResult(tool.ResponseType, contextLease.ShortCircuitResult, context);

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
            var structuredContent = SerializeStructuredResult(tool.ResponseType, effectiveResult, context);

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
            var fault = PluginExecutionResultBoxFromException();
            var structuredContent = SerializeStructuredResult(tool.ResponseType, fault, context);

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
        return new PluginExecutionResultBox
        {
            Outcome = stagedResult.Outcome,
            Data = stagedResult.Data,
            Changes = stagedResult.Changes,
            Diagnostics = stagedResult.Diagnostics,
            Warnings = stagedResult.Warnings,
            Error = stagedResult.Error,
            RequiredAction = stagedResult.RequiredAction,
        };
    }

    private static object DeserializeRequest(Type requestType, IDictionary<string, JsonElement> arguments)
    {
        var requestNode = new JsonObject();

        foreach (var pair in arguments)
        {
            requestNode[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
        }

        var request = requestNode.Deserialize(requestType, _serializerOptions);

        if (request is null)
        {
            throw new JsonException($"Request payload for '{requestType.FullName}' could not be deserialized.");
        }

        return request;
    }

    private static JsonElement SerializeStructuredResult(Type responseType, PluginExecutionResultBox result, IToolExecutionContext? context)
    {
        var toolResultType = typeof(ToolResult<>).MakeGenericType(responseType);
        var toolResult = CreateToolResult(toolResultType, result, context);

        return JsonSerializer.SerializeToElement(toolResult, toolResultType, _serializerOptions);
    }

    private static object CreateToolResult(Type toolResultType, PluginExecutionResultBox result, IToolExecutionContext? context)
    {
        var workspaceId = context?.WorkspaceIdentity?.WorkspaceId;
        var workspaceEpoch = context?.WorkspaceIdentity?.WorkspaceEpoch;
        var transactionRevision = result.Data is Contracts.Results.MutationData mutationData
            ? mutationData.Transaction?.Revision
            : context?.TransactionRevision;
        object?[] arguments;
        string methodName;

        switch (result.Outcome)
        {
            case ToolOutcome.Succeeded:
                methodName = nameof(ToolResult<object>.Succeeded);
                arguments =
                [
                    result.Data!,
                    workspaceId,
                    workspaceEpoch,
                    transactionRevision,
                    result.Changes,
                    result.Diagnostics,
                    result.Warnings,
                ];
                break;

            case ToolOutcome.NoChange:
                methodName = nameof(ToolResult<object>.NoChange);
                arguments =
                [
                    workspaceId,
                    workspaceEpoch,
                    transactionRevision,
                    result.Data,
                    result.Diagnostics,
                    result.Warnings,
                ];
                break;

            case ToolOutcome.Rejected:
                methodName = nameof(ToolResult<object>.Rejected);
                arguments =
                [
                    result.Error!,
                    result.RequiredAction,
                    workspaceId,
                    workspaceEpoch,
                    transactionRevision,
                    result.Diagnostics,
                    result.Warnings,
                ];
                break;

            case ToolOutcome.Conflict:
                methodName = nameof(ToolResult<object>.Conflict);
                arguments =
                [
                    result.Error!,
                    result.RequiredAction,
                    workspaceId,
                    workspaceEpoch,
                    transactionRevision,
                    result.Diagnostics,
                    result.Warnings,
                ];
                break;

            case ToolOutcome.Faulted:
                methodName = nameof(ToolResult<object>.Faulted);
                arguments =
                [
                    result.Error!,
                    result.RequiredAction,
                    workspaceId,
                    workspaceEpoch,
                    transactionRevision,
                    result.Diagnostics,
                    result.Warnings,
                ];
                break;

            default:
                throw new InvalidOperationException($"Unsupported tool outcome '{result.Outcome}'.");
        }

        return toolResultType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, arguments)!;
    }

    private static PluginExecutionResultBox PluginExecutionResultBoxFromException()
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Faulted,
            Error = new ToolError
            {
                Code = "UnhandledException",
                Message = "Tool execution failed.",
                CorrelationId = Guid.NewGuid().ToString("n"),
            },
        };
    }

    private static bool IsErrorOutcome(ToolOutcome outcome)
    {
        return outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted;
    }
}
