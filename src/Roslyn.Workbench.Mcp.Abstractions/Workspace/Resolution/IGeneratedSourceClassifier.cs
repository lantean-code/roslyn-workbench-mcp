namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Classifies checked-in source documents using the Host's generated-source conventions and exceptions.
/// </summary>
public interface IGeneratedSourceClassifier
{
    /// <summary>
    /// Determines whether a document appears generated and is not covered by a configured exception.
    /// </summary>
    /// <param name="document">The regular source document to classify.</param>
    /// <param name="workspaceRoot">The Workspace root used to evaluate relative-path exceptions.</param>
    /// <param name="cancellationToken">The token used to cancel source inspection.</param>
    /// <returns><see langword="true"/> when the document appears generated; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsGeneratedLookingAsync(
        Document document,
        string workspaceRoot,
        CancellationToken cancellationToken);
}
