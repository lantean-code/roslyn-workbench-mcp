namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceDocumentContentService
{
    ValueTask<WorkspaceDocumentContent> CreateAsync(
        Document document,
        CancellationToken cancellationToken);

    bool HasEquivalentContent(
        WorkspaceDocumentContent expected,
        WorkspaceDocumentContent candidate);
}
