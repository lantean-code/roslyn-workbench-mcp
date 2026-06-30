namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Identifies the Roslyn introduce-variable leaf action to stage.
/// </summary>
public enum IntroduceVariableKind
{
    /// <summary>
    /// Introduces a local for the selected expression.
    /// </summary>
    Local,

    /// <summary>
    /// Introduces a local for all supported occurrences of the selected expression.
    /// </summary>
    LocalAllOccurrences,

    /// <summary>
    /// Introduces a local constant for the selected expression.
    /// </summary>
    LocalConstant,

    /// <summary>
    /// Introduces a local constant for all supported occurrences of the selected expression.
    /// </summary>
    LocalConstantAllOccurrences,

    /// <summary>
    /// Introduces a constant field for the selected expression.
    /// </summary>
    Constant,

    /// <summary>
    /// Introduces a constant field for all supported occurrences of the selected expression.
    /// </summary>
    ConstantAllOccurrences,

    /// <summary>
    /// Introduces a field for the selected expression.
    /// </summary>
    Field,

    /// <summary>
    /// Introduces a field for all supported occurrences of the selected expression.
    /// </summary>
    FieldAllOccurrences,

    /// <summary>
    /// Introduces a query variable for the selected expression.
    /// </summary>
    QueryVariable,

    /// <summary>
    /// Introduces a query variable for all supported occurrences of the selected expression.
    /// </summary>
    QueryVariableAllOccurrences,
}
