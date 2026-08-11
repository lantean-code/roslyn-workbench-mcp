using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal sealed class PreparedSubmissionRetentionPolicy :
    IBoundedExpiringStorePolicy<string, PreparedSubmission>
{
    public int Capacity { get; }

    public PreparedSubmissionRetentionPolicy(IOptions<ErrorReportingOptions> options)
    {
        Capacity = options.Value.PreparedSubmissionCapacity;
    }

    public DateTimeOffset GetExpiration(PreparedSubmission value)
    {
        return value.ExpiresAt;
    }

    public bool TrySelectEvictionKey(
        IReadOnlyDictionary<string, PreparedSubmission> entries,
        [MaybeNullWhen(false)] out string key)
    {
        key = default;
        return false;
    }
}
