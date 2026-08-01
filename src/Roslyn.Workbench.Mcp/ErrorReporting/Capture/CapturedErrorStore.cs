using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed class CapturedErrorStore : ICapturedErrorStore
{
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<Guid, CapturedErrorRecord> _records = [];
    private readonly TimeProvider _timeProvider;
    private readonly int _capacity;

    public CapturedErrorStore(
        IOptions<ErrorReportingOptions> options,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _capacity = options.Value.CapturedErrorCapacity;
    }

    public void Add(CapturedErrorRecord record)
    {
        lock (_syncRoot)
        {
            RemoveExpiredLocked();
            while (_records.Count >= _capacity)
            {
                var oldest = _records.Values.MinBy(static item => item.FailureTime);
                if (oldest is null)
                {
                    break;
                }

                _records.Remove(oldest.CorrelationId);
            }

            _records[record.CorrelationId] = record;
        }
    }

    public bool TryGet(
        Guid correlationId,
        [NotNullWhen(true)] out CapturedErrorRecord? record)
    {
        lock (_syncRoot)
        {
            RemoveExpiredLocked();
            return _records.TryGetValue(correlationId, out record);
        }
    }

    private void RemoveExpiredLocked()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _records.ToArray())
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _records.Remove(pair.Key);
            }
        }
    }
}
