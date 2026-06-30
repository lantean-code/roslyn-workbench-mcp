namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Identifies the supported anonymous-type conversion variant to stage.
/// </summary>
public enum ConvertAnonymousTypeToClassKind
{
    /// <summary>
    /// Converts the anonymous type to a generated class.
    /// </summary>
    Class,

    /// <summary>
    /// Converts the anonymous type to a generated record.
    /// </summary>
    Record,
}
