namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Identifies the Roslyn extract-method variant to stage.
/// </summary>
public enum ExtractMethodTargetKind
{
    /// <summary>
    /// Extracts the selection into a method.
    /// </summary>
    Method,

    /// <summary>
    /// Extracts the selection into a local function.
    /// </summary>
    LocalFunction,
}
