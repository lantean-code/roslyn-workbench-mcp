namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the status of one runtime component reported by server diagnostics.
/// </summary>
internal sealed record ComponentStatus
{
    /// <summary>
    /// Whether the component is available.
    /// </summary>
    [Description("Whether the component is available.")]
    public bool IsAvailable { get; init; }

    /// <summary>
    /// The component version, when available.
    /// </summary>
    [Description("The component version, when available.")]
    public string? Version { get; init; }

    /// <summary>
    /// The optional status message.
    /// </summary>
    [Description("The optional status message.")]
    public string? Message { get; init; }
}
