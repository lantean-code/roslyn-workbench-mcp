using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Holds a plugin mutation context separately from the staging capability retained by the Host.
/// </summary>
internal sealed class PluginMutationExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceMutationExecutionLease _workspaceLease;

    private PluginMutationExecutionLease(
        WorkspaceMutationExecutionLease workspaceLease,
        IMutationContext? context,
        ToolExecutionFailureResult? failure)
    {
        _workspaceLease = workspaceLease;
        Context = context;
        Failure = failure;
    }

    /// <summary>
    /// Gets the plugin mutation context when acquisition succeeded.
    /// </summary>
    public IMutationContext? Context { get; }

    /// <summary>
    /// Gets the normalized acquisition failure when acquisition was rejected.
    /// </summary>
    public ToolExecutionFailureResult? Failure { get; }

    /// <summary>
    /// Gets a value indicating whether mutation context acquisition failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Context))]
    public bool HasFailure => Failure is not null;

    /// <summary>
    /// Stages a candidate returned by the plugin handler.
    /// </summary>
    /// <param name="operationName">The registered operation name.</param>
    /// <param name="candidate">The plugin mutation candidate.</param>
    /// <param name="diagnostics">Diagnostics produced by the handler.</param>
    /// <param name="warnings">Warnings produced by the handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized plugin mutation result.</returns>
    public async ValueTask<PluginExecutionResult<MutationData>> StageAsync(
        string operationName,
        MutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        if (_workspaceLease.HasFailure)
        {
            throw new InvalidOperationException("A rejected mutation lease cannot stage changes.");
        }

        var result = await _workspaceLease.Stager.StageAsync(
            operationName,
            new WorkspaceMutationCandidate
            {
                CandidateSolution = candidate.CandidateSolution,
                Summary = candidate.Summary,
                Warnings = candidate.Warnings,
            },
            diagnostics,
            warnings,
            cancellationToken);

        return PluginWorkspaceResultMapper.MapMutation(result);
    }

    /// <summary>
    /// Releases the underlying workspace mutation lease.
    /// </summary>
    /// <returns>A task representing asynchronous disposal.</returns>
    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }

    /// <summary>
    /// Creates a lease for a successfully acquired mutation context.
    /// </summary>
    /// <param name="workspaceLease">The underlying workspace mutation lease.</param>
    /// <param name="context">The plugin mutation context.</param>
    /// <returns>An acquired plugin mutation lease.</returns>
    public static PluginMutationExecutionLease Acquired(
        WorkspaceMutationExecutionLease workspaceLease,
        IMutationContext context)
    {
        return new PluginMutationExecutionLease(workspaceLease, context, null);
    }

    /// <summary>
    /// Creates a rejected lease while preserving any context available for failure projection.
    /// </summary>
    /// <param name="workspaceLease">The underlying rejected workspace lease.</param>
    /// <param name="failure">The normalized plugin execution failure.</param>
    /// <param name="context">The optional plugin context available despite rejection.</param>
    /// <returns>A rejected plugin mutation lease.</returns>
    public static PluginMutationExecutionLease Rejected(
        WorkspaceMutationExecutionLease workspaceLease,
        ToolExecutionFailureResult failure,
        IMutationContext? context = null)
    {
        return new PluginMutationExecutionLease(workspaceLease, context, failure);
    }
}
