namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Identifies which eligible methods should be replaced by a property.
/// </summary>
internal enum ReplaceMethodWithPropertyKind
{
    /// <summary>
    /// Replaces only the selected getter method.
    /// </summary>
    GetterOnly,

    /// <summary>
    /// Replaces the selected getter and its matching setter method.
    /// </summary>
    GetterAndSetter,
}
