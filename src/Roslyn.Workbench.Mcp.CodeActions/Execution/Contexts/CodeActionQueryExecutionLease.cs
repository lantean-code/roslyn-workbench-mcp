using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

/// <summary>
/// Owns the leased resources for Code Action query execution.
/// </summary>
internal sealed class CodeActionQueryExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceExecutionContextLease _workspaceLease;

    private CodeActionQueryExecutionLease(
        WorkspaceExecutionContextLease workspaceLease,
        ICodeActionQueryContext? context,
        CodeActionExecutionFailure? failure)
    {
        _workspaceLease = workspaceLease;
        Context = context;
        Failure = failure;
    }

    /// <summary>
    /// Gets the query context when workspace acquisition succeeds.
    /// </summary>
    public ICodeActionQueryContext? Context { get; }

    /// <summary>
    /// Gets the failure returned when workspace acquisition is rejected.
    /// </summary>
    public CodeActionExecutionFailure? Failure { get; }

    /// <summary>
    /// Gets a value indicating whether a failure prevented the operation from completing.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Context))]
    public bool HasFailure => Failure is not null;

    /// <summary>
    /// Releases the underlying workspace query lease.
    /// </summary>
    /// <returns>A task that completes when the workspace lease is released.</returns>
    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }

    /// <summary>
    /// Creates a successful lease over the acquired workspace and query context.
    /// </summary>
    /// <param name="workspaceLease">The lease that owns the acquired workspace resources.</param>
    /// <param name="context">The query context projected from the acquired workspace.</param>
    /// <returns>A lease that owns the acquired workspace resources.</returns>
    public static CodeActionQueryExecutionLease Acquired(
        WorkspaceExecutionContextLease workspaceLease,
        ICodeActionQueryContext context)
    {
        return new CodeActionQueryExecutionLease(workspaceLease, context, null);
    }

    /// <summary>
    /// Creates a rejected lease while preserving any context available for failure reporting.
    /// </summary>
    /// <param name="workspaceLease">The lease that owns the acquired workspace resources.</param>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    /// <param name="context">The optional query context available despite rejection.</param>
    /// <returns>A rejected lease that still owns the underlying workspace resources.</returns>
    public static CodeActionQueryExecutionLease Rejected(
        WorkspaceExecutionContextLease workspaceLease,
        CodeActionExecutionFailure failure,
        ICodeActionQueryContext? context = null)
    {
        return new CodeActionQueryExecutionLease(workspaceLease, context, failure);
    }
}
