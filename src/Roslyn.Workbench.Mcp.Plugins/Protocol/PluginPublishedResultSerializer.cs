using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Plugins.Execution;

namespace Roslyn.Workbench.Mcp.Plugins.Protocol;

internal static class PluginPublishedResultSerializer
{
    public static JsonElement SerializeFailure(ToolExecutionFailureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Outcome.IsError())
        {
            throw new InvalidOperationException($"Failure serialization requires an error outcome, but '{result.Outcome}' was supplied.");
        }

        return ToolResultEnvelopeSerializer.CreateFailure(result.Error, result.RequiredAction);
    }

    public static JsonElement SerializeMutation(PluginExecutionResult<MutationData> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Outcome.IsError())
        {
            return SerializeFailure(CreateFailureResult(
                result.Outcome,
                result.Error,
                result.RequiredAction,
                result.Diagnostics,
                result.Warnings,
                "Mutation"));
        }

        return ToolResultEnvelopeSerializer.CreateMutationSuccess(
            result.Data,
            staged: result.Outcome != ToolOutcome.NoChange && result.Data is not null);
    }

    public static JsonElement SerializeQuery<TResponse>(PluginExecutionResult<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Outcome.IsError())
        {
            return SerializeFailure(CreateFailureResult(
                result.Outcome,
                result.Error,
                result.RequiredAction,
                result.Diagnostics,
                result.Warnings,
                "Query"));
        }

        return ToolResultEnvelopeSerializer.CreateNestedSuccess("data", result.Data);
    }

    private static ToolExecutionFailureResult CreateFailureResult(
        ToolOutcome outcome,
        ToolError? error,
        RequiredAction? requiredAction,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        string toolKind)
    {
        return new ToolExecutionFailureResult
        {
            Outcome = outcome,
            Error = error ?? throw new InvalidOperationException($"{toolKind} failure result must provide an error."),
            RequiredAction = requiredAction,
            Diagnostics = diagnostics,
            Warnings = warnings,
        };
    }
}
