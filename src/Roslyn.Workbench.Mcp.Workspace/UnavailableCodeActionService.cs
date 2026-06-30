using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class UnavailableCodeActionService : ICodeActionService
{
    private readonly ComponentStatus _status;

    public UnavailableCodeActionService(string? message = null)
    {
        _status = new ComponentStatus
        {
            IsAvailable = false,
            Message = message ?? "Code-action composition is unavailable.",
        };
    }

    public ComponentStatus Status => _status;

    public ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<CodeActionListData>());
    }

    public ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<DescribeCodeActionData>());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<MutationProposal>());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<MutationProposal>());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<MutationProposal>());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<MutationProposal>());
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<MutationProposal>());
    }

    private static PluginExecutionResult<T> Rejected<T>()
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "Code-action composition is unavailable.",
        });
    }
}
