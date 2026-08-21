namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Represents the result of validating a snapshot precondition.
/// </summary>
public sealed record SnapshotMatchResult
{
    /// <summary>
    /// Gets the validation outcome.
    /// </summary>
    public SnapshotMatchKind Kind { get; init; }

    /// <summary>
    /// Creates a matched result.
    /// </summary>
    /// <returns>The match result.</returns>
    public static SnapshotMatchResult Matched()
    {
        return new SnapshotMatchResult
        {
            Kind = SnapshotMatchKind.Matched,
        };
    }

    /// <summary>
    /// Creates a workspace epoch mismatch result.
    /// </summary>
    /// <returns>The match result.</returns>
    public static SnapshotMatchResult WorkspaceEpochMismatch()
    {
        return new SnapshotMatchResult
        {
            Kind = SnapshotMatchKind.WorkspaceEpochMismatch,
        };
    }

    /// <summary>
    /// Creates an opaque snapshot identifier mismatch result.
    /// </summary>
    /// <returns>The match result.</returns>
    public static SnapshotMatchResult SnapshotIdMismatch()
    {
        return new SnapshotMatchResult
        {
            Kind = SnapshotMatchKind.SnapshotIdMismatch,
        };
    }

    /// <summary>
    /// Creates a transaction revision mismatch result.
    /// </summary>
    /// <returns>The match result.</returns>
    public static SnapshotMatchResult TransactionRevisionMismatch()
    {
        return new SnapshotMatchResult
        {
            Kind = SnapshotMatchKind.TransactionRevisionMismatch,
        };
    }
}
