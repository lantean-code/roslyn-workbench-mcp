namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceMutationDocumentIdentityComparer : IComparer<WorkspaceMutationDocumentIdentity>
{
    public static WorkspaceMutationDocumentIdentityComparer Instance { get; } = new();

    private WorkspaceMutationDocumentIdentityComparer()
    {
    }

    public int Compare(WorkspaceMutationDocumentIdentity? left, WorkspaceMutationDocumentIdentity? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var projectComparison = left.ProjectId.CompareTo(right.ProjectId);
        if (projectComparison != 0)
        {
            return projectComparison;
        }

        var pathModeComparison = left.DocumentPath.Comparison.CompareTo(right.DocumentPath.Comparison);
        if (pathModeComparison != 0)
        {
            return pathModeComparison;
        }

        var pathComparison = string.Compare(
            left.DocumentPath.Path,
            right.DocumentPath.Path,
            left.DocumentPath.Comparison);

        if (pathComparison != 0)
        {
            return pathComparison;
        }

        var changeKindComparison = left.ChangeKind.CompareTo(right.ChangeKind);
        if (changeKindComparison != 0)
        {
            return changeKindComparison;
        }

        var contentHashComparison = string.Compare(
            left.ContentHash,
            right.ContentHash,
            StringComparison.Ordinal);

        if (contentHashComparison != 0)
        {
            return contentHashComparison;
        }

        var serializedBytesHashComparison = string.Compare(
            left.SerializedBytesHash,
            right.SerializedBytesHash,
            StringComparison.Ordinal);

        if (serializedBytesHashComparison != 0)
        {
            return serializedBytesHashComparison;
        }

        return string.Compare(
            left.EncodingName,
            right.EncodingName,
            StringComparison.Ordinal);
    }
}
