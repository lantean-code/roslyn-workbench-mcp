namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents the effective non-sensitive server configuration.
/// </summary>
internal sealed record ServerConfiguration
{
    /// <summary>
    /// Gets the default maximum collection result count.
    /// </summary>
    public int DefaultMaxResults { get; init; }

    /// <summary>
    /// Gets the configured code-action reference lifetime.
    /// </summary>
    public TimeSpan CodeActionReferenceLifetime { get; init; }

    /// <summary>
    /// Gets the configured transaction revision capacity.
    /// </summary>
    public int MaxTransactionRevisions { get; init; }

    /// <summary>
    /// Gets the configured maximum concurrent query count.
    /// </summary>
    public int MaxConcurrentQueries { get; init; }

    /// <summary>
    /// Gets the configured output schema publication mode.
    /// </summary>
    public ToolOutputSchemaMode ToolOutputSchemaMode { get; init; }
}
