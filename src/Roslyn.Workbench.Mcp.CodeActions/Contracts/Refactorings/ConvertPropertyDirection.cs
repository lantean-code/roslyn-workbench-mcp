namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Selects the property-conversion direction for future Roslyn-backed property rewrites.
/// </summary>
public enum ConvertPropertyDirection
{
    /// <summary>
    /// Converts a supported auto-property to a full property.
    /// </summary>
    ToFull = 0,

    /// <summary>
    /// Converts a supported full property to an auto-property only when the rewrite is safe.
    /// </summary>
    ToAutoWhenSafe = 1,
}
