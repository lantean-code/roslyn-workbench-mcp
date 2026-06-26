namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the effective non-sensitive server configuration.
/// </summary>
public sealed record ServerConfiguration
{
    /// <summary>
    /// Gets the default maximum collection result count.
    /// </summary>
    public int DefaultMaxResults { get; init; }

    /// <summary>
    /// Gets the configured maximum response size in bytes.
    /// </summary>
    public int MaxResponseBytes { get; init; }

    /// <summary>
    /// Gets the configured code-action token lifetime.
    /// </summary>
    public TimeSpan CodeActionTokenLifetime { get; init; }

    /// <summary>
    /// Gets the configured transaction revision capacity.
    /// </summary>
    public int MaxTransactionRevisions { get; init; }

    /// <summary>
    /// Gets the configured maximum concurrent query count.
    /// </summary>
    public int MaxConcurrentQueries { get; init; }
}
