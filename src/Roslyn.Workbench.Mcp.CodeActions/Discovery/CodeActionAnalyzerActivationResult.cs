using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record CodeActionAnalyzerActivationResult
{
    public CodeActionAnalyzerActivationStatus Status { get; }

    public DiagnosticAnalyzer? Analyzer { get; }

    [MemberNotNullWhen(true, nameof(Analyzer))]
    public bool IsAvailable => Analyzer is not null;

    private CodeActionAnalyzerActivationResult(
        CodeActionAnalyzerActivationStatus status,
        DiagnosticAnalyzer? analyzer)
    {
        Status = status;
        Analyzer = analyzer;
    }

    public static CodeActionAnalyzerActivationResult Available(DiagnosticAnalyzer analyzer)
    {
        return new CodeActionAnalyzerActivationResult(
            CodeActionAnalyzerActivationStatus.Available,
            analyzer);
    }

    public static CodeActionAnalyzerActivationResult IncompatibleType()
    {
        return Unavailable(CodeActionAnalyzerActivationStatus.IncompatibleType);
    }

    public static CodeActionAnalyzerActivationResult ConstructionFailed()
    {
        return Unavailable(CodeActionAnalyzerActivationStatus.ConstructionFailed);
    }

    private static CodeActionAnalyzerActivationResult Unavailable(CodeActionAnalyzerActivationStatus status)
    {
        return new CodeActionAnalyzerActivationResult(status, analyzer: null);
    }
}
