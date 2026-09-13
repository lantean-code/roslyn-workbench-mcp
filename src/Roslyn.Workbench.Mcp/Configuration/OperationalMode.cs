namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Identifies a supported startup-selected operational policy profile.
/// </summary>
internal enum OperationalMode
{
    /// <summary>
    /// Publishes inspection capabilities without supported source mutation.
    /// </summary>
    InspectionOnly = 0,

    /// <summary>
    /// Enables transactions with Host-requested commit confirmation.
    /// </summary>
    Transactional = 1,

    /// <summary>
    /// Enables transactions with approval bound to a canonical review receipt.
    /// </summary>
    ApprovalRequired = 2,

    /// <summary>
    /// Enables transactions without Host-requested commit confirmation.
    /// </summary>
    AutonomousTrusted = 3,
}
