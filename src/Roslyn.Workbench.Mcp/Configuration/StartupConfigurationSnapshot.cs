namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Captures an immutable snapshot of startup configuration.
/// </summary>
internal sealed class StartupConfigurationSnapshot
{
    /// <summary>
    /// Gets the validated options used to configure the host.
    /// </summary>
    public required StartupOptions Options { get; init; }

    /// <summary>
    /// Gets recoverable configuration problems for which a safe fallback was selected.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];
}
