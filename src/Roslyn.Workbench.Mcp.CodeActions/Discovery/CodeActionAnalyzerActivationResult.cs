using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Represents an activated analyzer or the reason its type could not be activated.
/// </summary>
internal sealed record CodeActionAnalyzerActivationResult
{
    /// <summary>
    /// Gets the analyzer activation outcome.
    /// </summary>
    public CodeActionAnalyzerActivationStatus Status { get; }

    /// <summary>
    /// Gets the activated analyzer when available.
    /// </summary>
    public DiagnosticAnalyzer? Analyzer { get; }

    /// <summary>
    /// Gets a value indicating whether the requested capability is available.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Analyzer))]
    public bool IsAvailable => Analyzer is not null;

    private CodeActionAnalyzerActivationResult(
        CodeActionAnalyzerActivationStatus status,
        DiagnosticAnalyzer? analyzer)
    {
        Status = status;
        Analyzer = analyzer;
    }

    /// <summary>
    /// Creates a successful activation result.
    /// </summary>
    /// <param name="analyzer">The diagnostic analyzer created by successful activation.</param>
    /// <returns>A result that represents availability.</returns>
    public static CodeActionAnalyzerActivationResult Available(DiagnosticAnalyzer analyzer)
    {
        return new CodeActionAnalyzerActivationResult(
            CodeActionAnalyzerActivationStatus.Available,
            analyzer);
    }

    /// <summary>
    /// Creates a result that represents an incompatible provider type.
    /// </summary>
    /// <returns>A result that represents an incompatible provider type.</returns>
    public static CodeActionAnalyzerActivationResult IncompatibleType()
    {
        return Unavailable(CodeActionAnalyzerActivationStatus.IncompatibleType);
    }

    /// <summary>
    /// Creates a result that represents provider construction failure.
    /// </summary>
    /// <returns>A result that represents provider construction failure.</returns>
    public static CodeActionAnalyzerActivationResult ConstructionFailed()
    {
        return Unavailable(CodeActionAnalyzerActivationStatus.ConstructionFailed);
    }

    private static CodeActionAnalyzerActivationResult Unavailable(CodeActionAnalyzerActivationStatus status)
    {
        return new CodeActionAnalyzerActivationResult(status, analyzer: null);
    }
}
