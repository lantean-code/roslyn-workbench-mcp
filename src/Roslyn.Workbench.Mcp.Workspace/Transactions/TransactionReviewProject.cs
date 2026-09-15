namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies one project that owns a file represented by a transaction review receipt.
/// </summary>
internal sealed record TransactionReviewProject
{
    /// <summary>
    /// Gets the canonical Workspace-relative project path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the evaluated target framework when available.
    /// </summary>
    public string? TargetFramework { get; init; }
}
