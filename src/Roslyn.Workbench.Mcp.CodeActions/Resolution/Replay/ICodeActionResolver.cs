namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

internal interface ICodeActionResolver
{
    ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        Guid actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind? expectedKind,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
