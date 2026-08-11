namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

internal interface IBundledAsyncAnalyzerProvider
{
    IReadOnlyList<DiagnosticAnalyzer> Analyzers { get; }
}
