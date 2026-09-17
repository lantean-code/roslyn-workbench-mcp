namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Defines the compiler validation required before a transaction may be committed.
/// </summary>
internal enum CommitValidationPolicy
{
    /// <summary>
    /// Does not require compiler validation before commit.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requires the staged transaction to introduce no compiler errors relative to its baseline.
    /// </summary>
    NoNewCompilerErrors = 1,
}
