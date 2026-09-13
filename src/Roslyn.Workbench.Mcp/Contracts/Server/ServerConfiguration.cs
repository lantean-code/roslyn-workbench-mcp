namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the effective non-sensitive server configuration.
/// </summary>
internal sealed record ServerConfiguration
{
    /// <summary>
    /// The selected operational mode.
    /// </summary>
    [Description("The selected operational mode.")]
    public required string OperationalMode { get; init; }

    /// <summary>
    /// Whether source mutation is enabled.
    /// </summary>
    [Description("Whether source mutation is enabled.")]
    public bool SourceMutationEnabled { get; init; }

    /// <summary>
    /// Whether loading external plugin packages was explicitly enabled at startup.
    /// </summary>
    [Description("Whether loading external plugin packages was explicitly enabled at startup.")]
    public bool ExternalPluginsEnabled { get; init; }

    /// <summary>
    /// Whether Host confirmation is required before transaction commit.
    /// </summary>
    [Description("Whether Host confirmation is required before transaction commit.")]
    public bool CommitConfirmationRequired { get; init; }

    /// <summary>
    /// Whether receipt-bound approval is required before transaction commit.
    /// </summary>
    [Description("Whether receipt-bound approval is required before transaction commit.")]
    public bool ReceiptApprovalRequired { get; init; }

    /// <summary>
    /// Whether compiler validation is required before transaction commit.
    /// </summary>
    [Description("Whether compiler validation is required before transaction commit.")]
    public bool CompilerValidationRequired { get; init; }

    /// <summary>
    /// Whether the connected client advertised MCP elicitation, when known.
    /// </summary>
    [Description("Whether the connected client advertised MCP elicitation, when known.")]
    public bool? ClientSupportsElicitation { get; init; }

    /// <summary>
    /// The current process-local transaction commit-confirmation state.
    /// </summary>
    [Description("The current process-local transaction commit-confirmation state.")]
    public required string CommitConfirmationState { get; init; }

    /// <summary>
    /// Indicates whether Workspace admission is unrestricted or restricted by Host configuration.
    /// </summary>
    [Description("Indicates whether Workspace admission is unrestricted or restricted by Host configuration.")]
    public required string WorkspaceAdmission { get; init; }

    /// <summary>
    /// The number of effective configured Workspace authority roots.
    /// </summary>
    [Description("The number of effective configured Workspace authority roots.")]
    public int AllowedWorkspaceRootCount { get; init; }

    /// <summary>
    /// The policy applied to evaluated documents outside the effective Workspace root.
    /// </summary>
    [Description("The policy applied to evaluated documents outside the effective Workspace root.")]
    public required string ExternalDocumentPolicy { get; init; }

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
