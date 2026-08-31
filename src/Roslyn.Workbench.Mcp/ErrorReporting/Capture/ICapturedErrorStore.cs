using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Retains captured errors temporarily for later inspection or submission.
/// </summary>
internal interface ICapturedErrorStore
{
    /// <summary>
    /// Retains a captured error under its correlation identifier.
    /// </summary>
    /// <param name="record">The captured error record being projected or submitted.</param>
    void Add(CapturedErrorRecord record);

    /// <summary>
    /// Attempts to retrieve an unexpired captured error by correlation identifier.
    /// </summary>
    /// <param name="correlationId">The identifier assigned when the error was captured.</param>
    /// <param name="record">The retained error when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an unexpired record was found; otherwise, <see langword="false"/>.</returns>
    bool TryGet(Guid correlationId, [NotNullWhen(true)] out CapturedErrorRecord? record);
}
