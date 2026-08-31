namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Validates and constructs diagnostic analyzer types used for Code Fix discovery.
/// </summary>
internal interface ICodeActionAnalyzerActivator
{
    /// <summary>
    /// Creates a diagnostic analyzer from a compatible runtime analyzer type.
    /// </summary>
    /// <param name="analyzerType">The runtime type to validate and instantiate as a diagnostic analyzer.</param>
    /// <returns>The activated analyzer or a categorized activation failure.</returns>
    CodeActionAnalyzerActivationResult Activate(Type analyzerType);
}
