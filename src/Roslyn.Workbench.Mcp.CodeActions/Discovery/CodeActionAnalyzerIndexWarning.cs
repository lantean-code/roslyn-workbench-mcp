namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Identifies an analyzer type that could not be used by the built-in analyzer index.
/// </summary>
internal sealed record CodeActionAnalyzerIndexWarning
{
    /// <summary>
    /// Gets the assembly-qualified analyzer type name.
    /// </summary>
    public required string AnalyzerTypeName { get; init; }

    /// <summary>
    /// Gets the activation failure category.
    /// </summary>
    public required CodeActionAnalyzerActivationStatus Status { get; init; }
}
