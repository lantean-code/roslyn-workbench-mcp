namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Resolution;

internal interface ICodeActionResolutionService
{
    ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        string actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind? expectedKind,
        IToolExecutionContext context,
        CancellationToken cancellationToken);
}
