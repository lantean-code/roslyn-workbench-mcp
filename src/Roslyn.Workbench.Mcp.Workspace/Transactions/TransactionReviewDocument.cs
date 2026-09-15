namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes one validated file operation included in a transaction review receipt.
/// </summary>
internal sealed record TransactionReviewDocument
{
    /// <summary>
    /// Gets the canonical Workspace-relative source path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the file operation represented by the staged change.
    /// </summary>
    public required WorkspaceFileOperation Operation { get; init; }

    /// <summary>
    /// Gets whether the file existed at the transaction baseline.
    /// </summary>
    public required bool OriginalExists { get; init; }

    /// <summary>
    /// Gets the original serialized-byte hash when the file existed.
    /// </summary>
    public string? OriginalHash { get; init; }

    /// <summary>
    /// Gets the intended serialized-byte hash when the operation leaves a file present.
    /// </summary>
    public string? IntendedHash { get; init; }

    /// <summary>
    /// Gets the intended Unix file mode when applicable.
    /// </summary>
    public int? IntendedUnixFileMode { get; init; }

    /// <summary>
    /// Gets the stable projects that own this source file.
    /// </summary>
    public IReadOnlyList<TransactionReviewProject> Projects { get; init; } = [];

    /// <summary>
    /// Gets the definitive source classification used by current mutation policy.
    /// </summary>
    public string Classification { get; init; } = "project-source";

    /// <summary>
    /// Gets whether the physical target is contained by current Workspace authority.
    /// </summary>
    public bool Contained { get; init; } = true;

    /// <summary>
    /// Gets the bounded line-change summary produced for this document.
    /// </summary>
    public DiffSummary? LineSummary { get; init; }
}
