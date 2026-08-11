using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed class CapturedErrorRetentionPolicy :
    IBoundedExpiringStorePolicy<Guid, CapturedErrorRecord>
{
    public int Capacity { get; }

    public CapturedErrorRetentionPolicy(IOptions<ErrorReportingOptions> options)
    {
        Capacity = options.Value.CapturedErrorCapacity;
    }

    public DateTimeOffset GetExpiration(CapturedErrorRecord value)
    {
        return value.ExpiresAt;
    }

    public bool TrySelectEvictionKey(
        IReadOnlyDictionary<Guid, CapturedErrorRecord> entries,
        out Guid key)
    {
        var oldest = entries.First();
        foreach (var candidate in entries.Skip(1))
        {
            if (candidate.Value.FailureTime < oldest.Value.FailureTime)
            {
                oldest = candidate;
            }
        }

        key = oldest.Key;
        return true;
    }
}
