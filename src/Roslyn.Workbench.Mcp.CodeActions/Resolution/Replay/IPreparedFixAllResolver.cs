namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

internal interface IPreparedFixAllResolver
{
    ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        Guid actionId,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
