using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class McpPublishedResultSerializer
{
    public static JsonElement SerializePluginFailure(ToolExecutionFailureResult result)
    {

        if (!result.Outcome.IsError())
        {
            throw new InvalidOperationException($"Failure serialization requires an error outcome, but '{result.Outcome}' was supplied.");
        }

        return SerializeFailure(result.Error.Code, result.Error.Message, result.Error.CorrelationId, result.RequiredAction);
    }

    public static JsonElement SerializePluginQuery<TResponse>(PluginExecutionResult<TResponse> result)
    {

        return result.HasError
            ? SerializePluginFailure(CreatePluginFailure(result, result.Error))
            : ToolResultEnvelopeSerializer.CreateSuccess(result.Data);
    }

    public static JsonElement SerializePluginMutation(PluginExecutionResult<MutationData> result)
    {

        return result.HasError
            ? SerializePluginFailure(CreatePluginFailure(result, result.Error))
            : ToolResultEnvelopeSerializer.CreateMutationSuccess(
                result.Data,
                result.Outcome != PluginExecutionOutcome.NoChange && result.Data is not null);
    }

    public static JsonElement SerializeCodeActionFailure(CodeActionExecutionFailure result)
    {

        if (!result.Outcome.IsError())
        {
            throw new InvalidOperationException($"Failure serialization requires an error outcome, but '{result.Outcome}' was supplied.");
        }

        return SerializeFailure(result.Error.Code, result.Error.Message, result.Error.CorrelationId, result.RequiredAction);
    }

    public static JsonElement SerializeCodeActionQuery<TResponse>(CodeActionExecutionResult<TResponse> result)
    {

        return result.HasError
            ? SerializeCodeActionFailure(CreateCodeActionFailure(result, result.Error))
            : ToolResultEnvelopeSerializer.CreateSuccess(result.Data);
    }

    public static JsonElement SerializeCodeActionMutation(CodeActionExecutionResult<MutationData> result)
    {

        return result.HasError
            ? SerializeCodeActionFailure(CreateCodeActionFailure(result, result.Error))
            : ToolResultEnvelopeSerializer.CreateMutationSuccess(
                result.Data,
                result.Outcome != CodeActionExecutionOutcome.NoChange && result.Data is not null);
    }

    private static ToolExecutionFailureResult CreatePluginFailure<TResponse>(
        PluginExecutionResult<TResponse> result,
        PluginExecutionError error)
    {
        return new ToolExecutionFailureResult
        {
            Outcome = result.Outcome,
            Error = error,
            RequiredAction = result.RequiredAction,
            Diagnostics = result.Diagnostics,
            Warnings = result.Warnings,
        };
    }

    private static CodeActionExecutionFailure CreateCodeActionFailure<TResponse>(
        CodeActionExecutionResult<TResponse> result,
        CodeActionExecutionError error)
    {
        return new CodeActionExecutionFailure
        {
            Outcome = result.Outcome,
            Error = error,
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
