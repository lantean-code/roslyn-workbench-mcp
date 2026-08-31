using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Carries the status, context, diagnostics, and optional payload or error from a workspace operation.
/// </summary>
/// <typeparam name="TOutcome">The outcome type.</typeparam>
internal sealed class WorkspaceOperationResult<TOutcome>
{
    /// <summary>
    /// Gets the classified operation outcome.
    /// </summary>
    public WorkspaceOperationStatus Status { get; }

    /// <summary>
    /// Gets the workspace snapshot against which the operation ran.
    /// </summary>
    public WorkspaceOperationContext Context { get; }

    /// <summary>
    /// Gets the operation payload when the status carries a successful outcome.
    /// </summary>
    public TOutcome? Data { get; }

    /// <summary>
    /// Gets the structured error when the status represents a rejection, conflict, or fault.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    /// <summary>
    /// Gets diagnostics associated with the workspace operation.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Gets non-fatal warnings associated with the operation.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; }

    /// <summary>
    /// Gets a value indicating whether the result contains data.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Data))]
    public bool HasData => Data is not null;

    /// <summary>
    /// Gets a value indicating whether the result contains an error.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceOperationResult{TOutcome}"/> class.
    /// </summary>
    /// <param name="status">The classified operation outcome.</param>
    /// <param name="context">The workspace snapshot against which the operation ran.</param>
    /// <param name="data">The structured data to include in the result.</param>
    /// <param name="error">The structured error for an unsuccessful outcome.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    internal WorkspaceOperationResult(
        WorkspaceOperationStatus status,
        WorkspaceOperationContext context,
        TOutcome? data,
        WorkspaceOperationError? error,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        Status = status;
        Context = context;
        Data = data;
        Error = error;
        Diagnostics = diagnostics;
        Warnings = warnings;
    }
}

/// <summary>
/// Creates workspace-operation results whose payload and error state match their status.
/// </summary>
internal static class WorkspaceOperationResult
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
    public static WorkspaceOperationResult<TOutcome> Succeeded<TOutcome>(
        TOutcome data,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new WorkspaceOperationResult<TOutcome>(
            WorkspaceOperationStatus.Succeeded,
            context ?? new WorkspaceOperationContext(),
            data,
            error: null,
            diagnostics ?? [],
            warnings ?? []);
    }

    /// <summary>
    /// Creates a result that represents an unchanged workspace.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="data">The structured data to include in the result.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents an unchanged workspace.</returns>
    public static WorkspaceOperationResult<TOutcome> NoChange<TOutcome>(
        TOutcome? data = default,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new WorkspaceOperationResult<TOutcome>(
            WorkspaceOperationStatus.NoChange,
            context ?? new WorkspaceOperationContext(),
            data,
            error: null,
            diagnostics ?? [],
            warnings ?? []);
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
    public static WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed<TOutcome>(WorkspaceOperationStatus.Rejected, error, context, diagnostics, warnings);
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
    public static WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed<TOutcome>(WorkspaceOperationStatus.Conflict, error, context, diagnostics, warnings);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents failure.</returns>
    public static WorkspaceOperationResult<TOutcome> Faulted<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed<TOutcome>(WorkspaceOperationStatus.Faulted, error, context, diagnostics, warnings);
    }

    private static WorkspaceOperationResult<TOutcome> Failed<TOutcome>(
        WorkspaceOperationStatus status,
        WorkspaceOperationError error,
        WorkspaceOperationContext? context,
        IReadOnlyList<DiagnosticInfo>? diagnostics,
        IReadOnlyList<WarningInfo>? warnings)
    {
        return new WorkspaceOperationResult<TOutcome>(
            status,
            context ?? new WorkspaceOperationContext(),
            data: default,
            error,
            diagnostics ?? [],
            warnings ?? []);
    }
}
