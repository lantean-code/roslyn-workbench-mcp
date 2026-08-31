namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Creates workspace operation results with status-consistent payload and error state.
/// </summary>
internal sealed class WorkspaceOperationResultFactory : IWorkspaceOperationResultFactory
{
    /// <summary>
    /// Creates a result that represents successful completion.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="data">The structured data to include in the result.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents successful completion.</returns>
    public WorkspaceOperationResult<TOutcome> Succeeded<TOutcome>(
        TOutcome data,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return WorkspaceOperationResult.Succeeded(
            data,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
    }

    /// <summary>
    /// Creates a result that represents rejection.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="code">The stable code used to identify the reported condition.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents rejection.</returns>
    public WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Rejected<TOutcome>(CreateError(code, message, requiredAction), context, diagnostics, warnings);
    }

    /// <summary>
    /// Creates a result that represents rejection.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents rejection.</returns>
    public WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return WorkspaceOperationResult.Rejected<TOutcome>(
            error,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
    }

    /// <summary>
    /// Creates a result that represents a workspace conflict.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="code">The stable code used to identify the reported condition.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents a workspace conflict.</returns>
    public WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Conflict<TOutcome>(CreateError(code, message, requiredAction), context, diagnostics, warnings);
    }

    /// <summary>
    /// Creates a result that represents a workspace conflict.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents a workspace conflict.</returns>
    public WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return WorkspaceOperationResult.Conflict<TOutcome>(
            error,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="code">The stable code used to identify the reported condition.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents failure.</returns>
    public WorkspaceOperationResult<TOutcome> Faulted<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        var error = CreateError(code, message, requiredAction);

        return WorkspaceOperationResult.Faulted<TOutcome>(
            error,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
    }

    /// <summary>
    /// Creates a result that represents an unchanged workspace.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="data">The structured data to include in the result.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents an unchanged workspace.</returns>
    public WorkspaceOperationResult<TOutcome> NoChange<TOutcome>(
        WorkspaceOperationContext? context = null,
        TOutcome? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return WorkspaceOperationResult.NoChange(
            data,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
    }

    private static WorkspaceOperationError CreateError(string code, string message, RequiredAction? requiredAction)
    {
        return new WorkspaceOperationError
        {
            Code = code,
            Message = message,
            RequiredAction = requiredAction,
        };
    }
}
