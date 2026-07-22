namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal interface IScopedCodeFixCandidateResolver
{
    ValueTask<ScopedCodeFixCandidateResolution> ResolveAsync(
        ScopedCodeFixRequest request,
        IReadOnlyList<Document> documents,
        IWorkspaceResolver workspaceResolver,
        CancellationToken cancellationToken);
}
