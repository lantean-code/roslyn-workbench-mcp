namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents the structured payload returned by a staged mutation.
/// </summary>
public sealed record MutationData
{
    /// <summary>
    /// Gets the mutation operation name.
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concise summary of the staged mutation.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the active transaction info after staging the mutation.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }

    /// <summary>
    /// Gets the compact preview for the staged mutation.
    /// </summary>
    public MutationPreview? Preview { get; init; }
}
