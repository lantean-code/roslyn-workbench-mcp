namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal static class ProjectContextMatcher
{
    public static bool AreSiblingContexts(
        Project sourceProject,
        Project candidateProject,
        IWorkspacePathComparison pathComparison)
    {
        var sourceProjectPath = sourceProject.FilePath;
        var candidateProjectPath = candidateProject.FilePath;
        if (candidateProject.Id == sourceProject.Id
            || string.IsNullOrWhiteSpace(sourceProjectPath)
            || string.IsNullOrWhiteSpace(candidateProjectPath))
        {
            return false;
        }

        return pathComparison.CreateKey(sourceProjectPath)
            == pathComparison.CreateKey(candidateProjectPath);
    }

    public static IReadOnlyList<DocumentId> GetDocumentIds(
        Project project,
        string documentPath,
        IWorkspacePathComparison pathComparison)
    {
        var documentPathKey = pathComparison.CreateKey(documentPath);
        return project.Documents
            .Where(document => document.FilePath is not null
                && pathComparison.CreateKey(document.FilePath) == documentPathKey)
            .Select(static document => document.Id)
            .ToArray();
    }

    public static bool ContainsDocument(
        Project project,
        string documentPath,
        IWorkspacePathComparison pathComparison)
    {
        var documentPathKey = pathComparison.CreateKey(documentPath);
        return project.Documents.Any(document => document.FilePath is not null
            && pathComparison.CreateKey(document.FilePath) == documentPathKey);
    }
}
