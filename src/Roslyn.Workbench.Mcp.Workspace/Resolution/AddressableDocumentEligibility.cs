namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Excludes generated and intermediate-output documents from agent-facing operations.
/// </summary>
internal sealed class AddressableDocumentEligibility : IAddressableDocumentEligibility
{
    private const string _intermediateDirectoryName = "obj";

    private readonly IWorkspacePathComparison _pathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddressableDocumentEligibility"/> class.
    /// </summary>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    public AddressableDocumentEligibility(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    /// <summary>
    /// Determines whether a document is eligible for agent-facing selection and mutation.
    /// </summary>
    /// <param name="document">The document to classify.</param>
    /// <returns><see langword="true"/> when the document may be selected or mutated; otherwise, <see langword="false"/>.</returns>
    public bool IsAddressable(Document document)
    {
        var path = document.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var comparison = _pathComparison.GetComparison(path);
        var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (string.Equals(segment, _intermediateDirectoryName, comparison))
            {
                return false;
            }
        }

        return true;
    }
}
