namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Identifies the supported anonymous-type conversion variant to stage.
/// </summary>
internal enum ConvertAnonymousTypeToClassKind
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
