using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Enriches inspection results with surrounding source context and containing symbols.
/// </summary>
public interface IInspectionContextService
{
    /// <summary>
    /// Reads the trimmed source line that contains the supplied span.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="span">The target span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The trimmed source line, or <see langword="null" /> when the document is unavailable.</returns>
    ValueTask<string?> ReadContextAsync(
        Document? document,
        TextSpan span,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to resolve the containing symbol at the supplied position.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="position">The source position.</param>
    /// <param name="currentSolution">The current solution.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The containing symbol when one can be resolved; otherwise <see langword="null" />.</returns>
    ValueTask<ISymbol?> TryCreateContainingSymbolAsync(
        Document document,
        int position,
        Solution currentSolution,
        CancellationToken cancellationToken);
}
