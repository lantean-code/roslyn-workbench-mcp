using System.Diagnostics.CodeAnalysis;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed class CapturedErrorStore : ICapturedErrorStore
{
    private readonly IBoundedExpiringStore<Guid, CapturedErrorRecord> _entries;

    public CapturedErrorStore(IBoundedExpiringStore<Guid, CapturedErrorRecord> entries)
    {
        _entries = entries;
    }

    public void Add(CapturedErrorRecord record)
    {
        _entries.AddOrReplace(record.CorrelationId, record);
    }

    public bool TryGet(
        Guid correlationId,
        [NotNullWhen(true)] out CapturedErrorRecord? record)
    {
        return _entries.TryGet(correlationId, out record);
    }
}
