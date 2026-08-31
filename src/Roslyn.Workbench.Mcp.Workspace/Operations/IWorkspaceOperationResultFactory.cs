namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Creates consistently shaped success and failure results for workspace operations.
/// </summary>
internal interface IWorkspaceOperationResultFactory
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
    WorkspaceOperationResult<TOutcome> Succeeded<TOutcome>(
        TOutcome data,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

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
    WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    /// <summary>
    /// Creates a result that represents rejection.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents rejection.</returns>
    WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

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
    WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    /// <summary>
    /// Creates a result that represents a workspace conflict.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents a workspace conflict.</returns>
    WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

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
    WorkspaceOperationResult<TOutcome> Faulted<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    /// <summary>
    /// Creates a result that represents an unchanged workspace.
    /// </summary>
    /// <typeparam name="TOutcome">The outcome type.</typeparam>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="data">The structured data to include in the result.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents an unchanged workspace.</returns>
    WorkspaceOperationResult<TOutcome> NoChange<TOutcome>(
        WorkspaceOperationContext? context = null,
        TOutcome? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);
}
