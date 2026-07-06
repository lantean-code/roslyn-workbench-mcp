using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace;

internal interface IWorkspaceOperationResultFactory
{
    WorkspaceOperationResult<TOutcome> Succeeded<TOutcome>(
        TOutcome data,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    WorkspaceOperationResult<TOutcome> Faulted<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);

    WorkspaceOperationResult<TOutcome> NoChange<TOutcome>(
        WorkspaceOperationContext? context = null,
        TOutcome? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null);
}
