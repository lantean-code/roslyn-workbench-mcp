namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class AddressableDocumentEligibility : IAddressableDocumentEligibility
{
    private const string _intermediateDirectoryName = "obj";

    private readonly IWorkspacePathComparison _pathComparison;

    public AddressableDocumentEligibility(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

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
