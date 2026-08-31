using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Applies configured capacity limits and oldest-first eviction to captured errors.
/// </summary>
internal sealed class CapturedErrorRetentionPolicy :
    IBoundedExpiringStorePolicy<Guid, CapturedErrorRecord>
{
    /// <summary>
    /// Gets the maximum number of captured errors retained locally.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CapturedErrorRetentionPolicy"/> class.
    /// </summary>
    /// <param name="options">The configured captured-error retention limits.</param>
    public CapturedErrorRetentionPolicy(IOptions<ErrorReportingOptions> options)
    {
        Capacity = options.Value.CapturedErrorCapacity;
    }

    /// <summary>
    /// Gets the time at which a captured error must be removed.
    /// </summary>
    /// <param name="value">The captured error being evaluated.</param>
    /// <returns>The record's configured expiration time.</returns>
    public DateTimeOffset GetExpiration(CapturedErrorRecord value)
    {
        return value.ExpiresAt;
    }

    /// <summary>
    /// Selects the oldest captured failure for eviction.
    /// </summary>
    /// <param name="entries">The captured errors currently retained by the store.</param>
    /// <param name="key">The correlation identifier of the oldest captured failure.</param>
    /// <returns><see langword="true"/> because a non-empty store always has an oldest failure.</returns>
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
