namespace Roslyn.Workbench.Mcp.Workspace.Configuration;

/// <summary>
/// Defines how generated-looking checked-in source is handled by mutation workflows.
/// </summary>
internal enum GeneratedSourcePolicy
{
    /// <summary>
    /// Allows the mutation and reports a warning for review.
    /// </summary>
    Warn = 0,

    /// <summary>
    /// Rejects the mutation unless the source path matches a configured exception.
    /// </summary>
    Deny = 1,
}
