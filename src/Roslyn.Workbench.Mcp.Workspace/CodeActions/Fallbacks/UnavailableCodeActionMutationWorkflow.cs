namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Fallbacks;

internal sealed class UnavailableCodeActionMutationWorkflow : Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution.ICodeActionMutationWorkflow
{
    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected());
    }

    private static PluginExecutionResult<MutationProposal> Rejected()
    {
        return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "Code-action composition is unavailable.",
        });
    }
}
