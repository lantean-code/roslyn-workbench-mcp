using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Converts plugin and Code Action execution results into the common MCP response envelopes.
/// </summary>
internal static class McpPublishedResultSerializer
{
    /// <summary>
    /// Serializes a failed plugin invocation.
    /// </summary>
    /// <param name="result">The failed plugin result.</param>
    /// <returns>The published failure envelope.</returns>
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

    /// <summary>
    /// Serializes a plugin query result against the snapshot it inspected.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="result">The plugin query result.</param>
    /// <param name="snapshot">The workspace snapshot against which the operation runs.</param>
    /// <returns>A success or failure envelope for the query.</returns>
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

    /// <summary>
    /// Serializes a plugin mutation result, including whether it staged a change.
    /// </summary>
    /// <param name="result">The plugin mutation result.</param>
    /// <param name="currentSnapshot">The current workspace snapshot attached to the mutation result.</param>
    /// <returns>A success or failure envelope for the mutation.</returns>
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

    /// <summary>
    /// Serializes a failed Code Action invocation.
    /// </summary>
    /// <param name="result">The failed Code Action result.</param>
    /// <returns>The published failure envelope.</returns>
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

    /// <summary>
    /// Serializes a Code Action query result against the snapshot it inspected.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="result">The Code Action query result.</param>
    /// <param name="snapshot">The workspace snapshot against which the operation runs.</param>
    /// <returns>A success or failure envelope for the query.</returns>
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

    /// <summary>
    /// Serializes a Code Action mutation result, including whether it staged a change.
    /// </summary>
    /// <param name="result">The Code Action mutation result.</param>
    /// <param name="currentSnapshot">The current workspace snapshot attached to the mutation result.</param>
    /// <returns>A success or failure envelope for the mutation.</returns>
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
