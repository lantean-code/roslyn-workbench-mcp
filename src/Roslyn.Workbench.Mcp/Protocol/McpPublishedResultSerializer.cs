using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class McpPublishedResultSerializer
{
    public static JsonElement SerializePluginFailure(ToolExecutionFailureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Outcome.IsError())
        {
            throw new InvalidOperationException($"Failure serialization requires an error outcome, but '{result.Outcome}' was supplied.");
        }

        return SerializeFailure(result.Error.Code, result.Error.Message, result.Error.CorrelationId, result.RequiredAction);
    }

    public static JsonElement SerializePluginQuery<TResponse>(PluginExecutionResult<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome.IsError()
            ? SerializePluginFailure(CreatePluginFailure(result, "Query"))
            : ToolResultEnvelopeSerializer.CreateNestedSuccess("data", result.Data);
    }

    public static JsonElement SerializePluginMutation(PluginExecutionResult<MutationData> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome.IsError()
            ? SerializePluginFailure(CreatePluginFailure(result, "Mutation"))
            : ToolResultEnvelopeSerializer.CreateMutationSuccess(
                result.Data,
                result.Outcome != PluginExecutionOutcome.NoChange && result.Data is not null);
    }

    public static JsonElement SerializeCodeActionFailure(CodeActionExecutionFailure result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Outcome.IsError())
        {
            throw new InvalidOperationException($"Failure serialization requires an error outcome, but '{result.Outcome}' was supplied.");
        }

        return SerializeFailure(result.Error.Code, result.Error.Message, result.Error.CorrelationId, result.RequiredAction);
    }

    public static JsonElement SerializeCodeActionQuery<TResponse>(CodeActionExecutionResult<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome.IsError()
            ? SerializeCodeActionFailure(CreateCodeActionFailure(result, "Query"))
            : ToolResultEnvelopeSerializer.CreateNestedSuccess("data", result.Data);
    }

    public static JsonElement SerializeCodeActionMutation(CodeActionExecutionResult<MutationData> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome.IsError()
            ? SerializeCodeActionFailure(CreateCodeActionFailure(result, "Mutation"))
            : ToolResultEnvelopeSerializer.CreateMutationSuccess(
                result.Data,
                result.Outcome != CodeActionExecutionOutcome.NoChange && result.Data is not null);
    }

    private static ToolExecutionFailureResult CreatePluginFailure<TResponse>(
        PluginExecutionResult<TResponse> result,
        string toolKind)
    {
        return new ToolExecutionFailureResult
        {
            Outcome = result.Outcome,
            Error = result.Error
                ?? throw new InvalidOperationException($"{toolKind} failure result must provide an error."),
            RequiredAction = result.RequiredAction,
            Diagnostics = result.Diagnostics,
            Warnings = result.Warnings,
        };
    }

    private static CodeActionExecutionFailure CreateCodeActionFailure<TResponse>(
        CodeActionExecutionResult<TResponse> result,
        string toolKind)
    {
        return new CodeActionExecutionFailure
        {
            Outcome = result.Outcome,
            Error = result.Error
                ?? throw new InvalidOperationException($"{toolKind} failure result must provide an error."),
            RequiredAction = result.RequiredAction,
        };
    }

    private static JsonElement SerializeFailure(
        string code,
        string message,
        string? correlationId,
        RequiredAction? requiredAction)
    {
        return ToolResultEnvelopeSerializer.CreateFailure(
            new ToolError
            {
                Code = code,
                Message = message,
                CorrelationId = correlationId,
            },
            requiredAction);
    }
}
