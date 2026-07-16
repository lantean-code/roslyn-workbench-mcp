using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record CodeActionAnalyzerActivationResult
{
    public required CodeActionAnalyzerActivationStatus Status { get; init; }

    public DiagnosticAnalyzer? Analyzer { get; init; }

    [MemberNotNullWhen(true, nameof(Analyzer))]
    public bool IsAvailable => Analyzer is not null;
}
