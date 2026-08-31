using System.Diagnostics.CodeAnalysis;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Retains captured errors by correlation identifier subject to expiration and capacity limits.
/// </summary>
internal sealed class CapturedErrorStore : ICapturedErrorStore
{
    private readonly IBoundedExpiringStore<Guid, CapturedErrorRecord> _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapturedErrorStore"/> class.
    /// </summary>
    /// <param name="entries">The bounded store used to retain captured errors.</param>
    public CapturedErrorStore(IBoundedExpiringStore<Guid, CapturedErrorRecord> entries)
    {
        _entries = entries;
    }

    /// <summary>
    /// Retains a captured error for later inspection or submission.
    /// </summary>
    /// <param name="record">The captured error record being projected or submitted.</param>
    public void Add(CapturedErrorRecord record)
    {
        _entries.AddOrReplace(record.CorrelationId, record);
    }

    /// <summary>
    /// Attempts to retrieve an unexpired captured error by correlation identifier.
    /// </summary>
    /// <param name="correlationId">The identifier assigned when the error was captured.</param>
    /// <param name="record">The retained error when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an unexpired record was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(
        Guid correlationId,
        [NotNullWhen(true)] out CapturedErrorRecord? record)
    {
        return _entries.TryGet(correlationId, out record);
    }
}
