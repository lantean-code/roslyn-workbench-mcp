using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Applies configured capacity and expiration limits without evicting still-reviewable submissions.
/// </summary>
internal sealed class PreparedSubmissionRetentionPolicy :
    IBoundedExpiringStorePolicy<string, PreparedSubmission>
{
    /// <summary>
    /// Gets the maximum number of prepared submissions retained at once.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PreparedSubmissionRetentionPolicy"/> class.
    /// </summary>
    /// <param name="options">The configured prepared-submission retention limits.</param>
    public PreparedSubmissionRetentionPolicy(IOptions<ErrorReportingOptions> options)
    {
        Capacity = options.Value.PreparedSubmissionCapacity;
    }

    /// <summary>
    /// Gets the time at which a prepared submission becomes unusable.
    /// </summary>
    /// <param name="value">The prepared submission being evaluated.</param>
    /// <returns>The submission handle's expiration time.</returns>
    public DateTimeOffset GetExpiration(PreparedSubmission value)
    {
        return value.ExpiresAt;
    }

    /// <summary>
    /// Refuses capacity eviction so a prepared payload is never silently replaced before expiry.
    /// </summary>
    /// <param name="entries">The prepared submissions currently retained by the store.</param>
    /// <param name="key">Always receives the default key because no submission is selected.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool TrySelectEvictionKey(
        IReadOnlyDictionary<string, PreparedSubmission> entries,
        [MaybeNullWhen(false)] out string key)
    {
        key = default;
        return false;
    }
}
