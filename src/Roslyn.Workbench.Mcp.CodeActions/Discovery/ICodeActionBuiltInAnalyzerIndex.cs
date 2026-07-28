using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionBuiltInAnalyzerIndex
{
    ImmutableArray<CodeActionAnalyzerIndexWarning> Warnings { get; }

    ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(IReadOnlySet<string> diagnosticIds);
}
