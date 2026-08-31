namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Matches sibling project contexts and their shared physical documents.
/// </summary>
internal static class ProjectContextMatcher
{
    /// <summary>
    /// Determines whether the projects are sibling target-framework contexts.
    /// </summary>
    /// <param name="sourceProject">The project context to compare with the candidate sibling context.</param>
    /// <param name="candidateProject">The project context being tested as a sibling of the source project.</param>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    /// <returns><see langword="true"/> when both projects represent sibling target-framework contexts; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Gets every document in a project that represents a physical path.
    /// </summary>
    /// <param name="project">The project to search.</param>
    /// <param name="documentPath">The document path whose matching identifiers are required.</param>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    /// <returns>The matching Roslyn document identifiers.</returns>
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

    /// <summary>
    /// Determines whether the project contains the supplied document.
    /// </summary>
    /// <param name="project">The project to search.</param>
    /// <param name="documentPath">The document path whose matching project contexts are required.</param>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    /// <returns><see langword="true"/> when the project contains a document at the path; otherwise, <see langword="false"/>.</returns>
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
