namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal interface ICodeActionResolutionService
{
    ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        string actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind? expectedKind,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
