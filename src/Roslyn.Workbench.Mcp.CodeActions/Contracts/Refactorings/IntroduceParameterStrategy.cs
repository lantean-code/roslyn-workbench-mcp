namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Identifies the Roslyn introduce-parameter strategy to stage.
/// </summary>
public enum IntroduceParameterStrategy
{
    /// <summary>
    /// Updates existing call sites directly.
    /// </summary>
    UpdateCallSitesDirectly,

    /// <summary>
    /// Extracts a helper method that the existing call sites invoke.
    /// </summary>
    IntoExtractedMethod,

    /// <summary>
    /// Introduces a new overload that preserves the existing call sites.
    /// </summary>
    IntoNewOverload,
}
