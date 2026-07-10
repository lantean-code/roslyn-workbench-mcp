namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Identifies the Roslyn foreach or LINQ conversion variant to stage.
/// </summary>
public enum ConvertForeachLinqKind
{
    /// <summary>
    /// Converts a supported foreach loop into query-expression LINQ syntax.
    /// </summary>
    ForeachToQuery,

    /// <summary>
    /// Converts a supported foreach loop into method-call LINQ syntax.
    /// </summary>
    ForeachToCallForm,

    /// <summary>
    /// Converts a supported query-expression LINQ expression into foreach syntax.
    /// </summary>
    LinqToForeach,
}
