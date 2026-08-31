namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the effective non-sensitive server configuration.
/// </summary>
internal sealed record ServerConfiguration
{
    /// <summary>
    /// The default maximum collection result count.
    /// </summary>
    [Description("The default maximum collection result count.")]
    public int DefaultMaxResults { get; init; }

    /// <summary>
    /// The configured code-action reference lifetime.
    /// </summary>
    [Description("The configured code-action reference lifetime.")]
    public TimeSpan CodeActionReferenceLifetime { get; init; }

    /// <summary>
    /// The configured transaction revision capacity.
    /// </summary>
    [Description("The configured transaction revision capacity.")]
    public int MaxTransactionRevisions { get; init; }

    /// <summary>
    /// The configured maximum concurrent query count.
    /// </summary>
    [Description("The configured maximum concurrent query count.")]
    public int MaxConcurrentQueries { get; init; }

    /// <summary>
    /// The configured output schema publication mode.
    /// </summary>
    [Description("The configured output schema publication mode.")]
    public ToolOutputSchemaMode ToolOutputSchemaMode { get; init; }

    /// <summary>
    /// The effective non-sensitive error-reporting configuration and session state.
    /// </summary>
    [Description("The effective non-sensitive error-reporting configuration and session state.")]
    public ErrorReportingStatusData? ErrorReporting { get; init; }
}
