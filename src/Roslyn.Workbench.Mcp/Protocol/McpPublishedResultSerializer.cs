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

        return SerializeFailure(
            result.Error.Code,
            result.Error.Message,
            result.Error.CorrelationId,
            result.RequiredAction,
            result.Diagnostics,
            result.Warnings);
    }

    public static JsonElement SerializePluginQuery<TResponse>(
        PluginExecutionResult<TResponse> result,
        SnapshotPrecondition snapshot)
    {
        if (result.HasError)
        {
            var failure = CreatePluginFailure(result, result.Error);
            return SerializePluginFailure(failure);
        }

        return ToolResultEnvelopeSerializer.CreateSuccess(result.Data, snapshot);
    }

    public static JsonElement SerializePluginMutation(
        PluginExecutionResult<MutationData> result,
        SnapshotPrecondition currentSnapshot)
    {
        if (result.HasError)
        {
            var failure = CreatePluginFailure(result, result.Error);
            return SerializePluginFailure(failure);
        }

        var staged = result.Outcome != PluginExecutionOutcome.NoChange && result.Data is not null;
        return ToolResultEnvelopeSerializer.CreateMutationSuccess(
            result.Data,
            staged,
            currentSnapshot);
    }

    public static JsonElement SerializeCodeActionFailure(CodeActionExecutionFailure result)
    {
        if (!result.Outcome.IsError())
        {
            throw new InvalidOperationException($"Failure serialization requires an error outcome, but '{result.Outcome}' was supplied.");
        }

        return SerializeFailure(
            result.Error.Code,
            result.Error.Message,
            result.Error.CorrelationId,
            result.RequiredAction,
            result.Diagnostics,
            result.Warnings);
    }

    public static JsonElement SerializeCodeActionQuery<TResponse>(
        CodeActionExecutionResult<TResponse> result,
        SnapshotPrecondition snapshot)
    {
        if (result.HasError)
        {
            var failure = CreateCodeActionFailure(result, result.Error);
            return SerializeCodeActionFailure(failure);
        }

        return ToolResultEnvelopeSerializer.CreateSuccess(result.Data, snapshot);
    }

    public static JsonElement SerializeCodeActionMutation(
        CodeActionExecutionResult<MutationData> result,
        SnapshotPrecondition currentSnapshot)
    {
        if (result.HasError)
        {
            var failure = CreateCodeActionFailure(result, result.Error);
            return SerializeCodeActionFailure(failure);
        }

        var staged = result.Outcome != CodeActionExecutionOutcome.NoChange && result.Data is not null;
        return ToolResultEnvelopeSerializer.CreateMutationSuccess(
            result.Data,
            staged,
            currentSnapshot);
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
            Diagnostics = result.Diagnostics,
            Warnings = result.Warnings,
        };
    }

    private static JsonElement SerializeFailure(
        string code,
        string message,
        string? correlationId,
        RequiredAction? requiredAction,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        var error = new ToolError
        {
            Code = code,
            Message = message,
            CorrelationId = correlationId,
        };

        return ToolResultEnvelopeSerializer.CreateFailure(error, requiredAction, diagnostics, warnings);
    }
}
