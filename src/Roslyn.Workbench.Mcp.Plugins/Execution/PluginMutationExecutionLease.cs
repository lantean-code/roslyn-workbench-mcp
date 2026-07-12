namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Holds a plugin mutation context separately from the staging capability retained by Host.
/// </summary>
public sealed class PluginMutationExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceMutationExecutionLease _workspaceLease;

    internal PluginMutationExecutionLease(
        WorkspaceMutationExecutionLease workspaceLease,
        IMutationContext? context,
        ToolExecutionFailureResult? failure)
    {
        _workspaceLease = workspaceLease;
        Context = context;
        Failure = failure;
    }

    /// <summary>Gets the plugin mutation context when acquisition succeeded.</summary>
    public IMutationContext? Context { get; }

    /// <summary>Gets the normalized acquisition failure when acquisition was rejected.</summary>
    public ToolExecutionFailureResult? Failure { get; }

    /// <summary>Stages a candidate returned by the plugin handler.</summary>
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
        var stager = _workspaceLease.Stager
            ?? throw new InvalidOperationException("Mutation context acquisition completed without a mutation stager.");
        var result = await stager.StageAsync(
            operationName,
            new WorkspaceMutationCandidate
            {
                CandidateSolution = candidate.CandidateSolution,
                Summary = candidate.Summary,
                Warnings = candidate.Warnings,
            },
            diagnostics,
            warnings,
            cancellationToken).ConfigureAwait(false);

        return PluginWorkspaceResultMapper.MapMutation(result);
    }

    /// <summary>Releases the underlying Workspace mutation lease.</summary>
    /// <returns>A task representing asynchronous disposal.</returns>
    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }
}
