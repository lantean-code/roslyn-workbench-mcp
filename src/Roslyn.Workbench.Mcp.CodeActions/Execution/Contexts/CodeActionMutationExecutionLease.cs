using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

/// <summary>
/// Owns the leased resources for Code Action mutation execution.
/// </summary>
internal sealed class CodeActionMutationExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceMutationExecutionLease _workspaceLease;

    private CodeActionMutationExecutionLease(
        WorkspaceMutationExecutionLease workspaceLease,
        ICodeActionMutationContext? context,
        CodeActionExecutionFailure? failure)
    {
        _workspaceLease = workspaceLease;
        Context = context;
        Failure = failure;
    }

    /// <summary>
    /// Gets the mutation context when workspace acquisition succeeds.
    /// </summary>
    public ICodeActionMutationContext? Context { get; }

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
    /// Stages the candidate solution through the acquired workspace lease and maps the staging result for the Code Action tool.
    /// </summary>
    /// <param name="operationName">The operation name recorded for the staged mutation.</param>
    /// <param name="candidate">The candidate solution and snapshot from Code Action execution.</param>
    /// <param name="diagnostics">The diagnostics to return with the staged mutation.</param>
    /// <param name="warnings">The warnings to return with the staged mutation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the mapped staging result, including diagnostics and warnings.</returns>
    public async ValueTask<CodeActionExecutionResult<MutationData>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        if (_workspaceLease.HasFailure)
        {
            throw new InvalidOperationException("A rejected Code Action mutation lease cannot stage changes.");
        }

        var result = await _workspaceLease.Stager.StageAsync(
            operationName,
            candidate,
            diagnostics,
            warnings,
            cancellationToken);

        return CodeActionWorkspaceResultMapper.MapMutation(result);
    }

    /// <summary>
    /// Releases the underlying workspace mutation lease.
    /// </summary>
    /// <returns>A task that completes when the workspace lease is released.</returns>
    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }

    /// <summary>
    /// Creates a successful lease over the acquired workspace and mutation context.
    /// </summary>
    /// <param name="workspaceLease">The lease that owns the acquired workspace resources.</param>
    /// <param name="context">The mutation context projected from the acquired workspace.</param>
    /// <returns>A lease that owns the acquired workspace resources.</returns>
    public static CodeActionMutationExecutionLease Acquired(
        WorkspaceMutationExecutionLease workspaceLease,
        ICodeActionMutationContext context)
    {
        return new CodeActionMutationExecutionLease(workspaceLease, context, null);
    }

    /// <summary>
    /// Creates a rejected lease while preserving any context available for failure reporting.
    /// </summary>
    /// <param name="workspaceLease">The lease that owns the acquired workspace resources.</param>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    /// <param name="context">The optional mutation context available despite rejection.</param>
    /// <returns>A rejected lease that still owns the underlying workspace resources.</returns>
    public static CodeActionMutationExecutionLease Rejected(
        WorkspaceMutationExecutionLease workspaceLease,
        CodeActionExecutionFailure failure,
        ICodeActionMutationContext? context = null)
    {
        return new CodeActionMutationExecutionLease(workspaceLease, context, failure);
    }
}
