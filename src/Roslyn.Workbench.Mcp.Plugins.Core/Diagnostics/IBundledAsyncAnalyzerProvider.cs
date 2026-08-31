namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

/// <summary>
/// Provides the analyzers used by the bundled async-code inspection.
/// </summary>
internal interface IBundledAsyncAnalyzerProvider
{
    /// <summary>
    /// Gets the bundled async analyzers.
    /// </summary>
    IReadOnlyList<DiagnosticAnalyzer> Analyzers { get; }
}
