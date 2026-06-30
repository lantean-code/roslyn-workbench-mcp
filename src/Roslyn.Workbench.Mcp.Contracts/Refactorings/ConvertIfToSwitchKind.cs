namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Selects the convert-if-to-switch replay variant.
/// </summary>
public enum ConvertIfToSwitchKind
{
    /// <summary>
    /// Converts to a switch statement.
    /// </summary>
    Statement = 0,

    /// <summary>
    /// Converts to a switch expression.
    /// </summary>
    Expression = 1,
}
