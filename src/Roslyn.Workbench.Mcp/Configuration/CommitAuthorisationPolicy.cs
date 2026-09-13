namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Defines the Host interaction required before a transaction commit.
/// </summary>
internal enum CommitAuthorisationPolicy
{
    /// <summary>
    /// Allows commit without Host-requested interaction.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requires simple commit confirmation.
    /// </summary>
    Confirmation = 1,

    /// <summary>
    /// Requires approval bound to a canonical transaction receipt.
    /// </summary>
    ReceiptApproval = 2,
}
