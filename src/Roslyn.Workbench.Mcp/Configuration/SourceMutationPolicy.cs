namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Defines whether supported source mutation capabilities are enabled.
/// </summary>
internal enum SourceMutationPolicy
{
    /// <summary>
    /// Prevents transaction acquisition and mutation staging.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Allows supported source mutation through the transaction pipeline.
    /// </summary>
    Enabled = 1,
}
