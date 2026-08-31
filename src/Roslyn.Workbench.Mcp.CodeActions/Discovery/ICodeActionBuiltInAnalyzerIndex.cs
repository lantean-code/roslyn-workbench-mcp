using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Indexes built-in analyzer types by the diagnostics they can produce.
/// </summary>
internal interface ICodeActionBuiltInAnalyzerIndex
{
    /// <summary>
    /// Gets analyzer types that could not be inspected or activated.
    /// </summary>
    ImmutableArray<CodeActionAnalyzerIndexWarning> Warnings { get; }

    /// <summary>
    /// Gets activated analyzers that may produce any requested diagnostic identifier.
    /// </summary>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <returns>The distinct matching analyzer instances.</returns>
    ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(IReadOnlySet<string> diagnosticIds);
}
