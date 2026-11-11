namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Supplies bounded, private caching hints for startup catalogues and embedded skill resources.
/// </summary>
internal static class CatalogueResponseCachePolicy
{
    /// <summary>
    /// Marks a completed startup catalogue page as fresh for one minute.
    /// </summary>
    /// <param name="result">The completed catalogue page.</param>
    /// <param name="request">The parameters used to obtain the page.</param>
    /// <param name="protocolVersion">The protocol version selected for the session.</param>
    public static void ApplyCatalogueHints(ICacheableResult result, RequestParams? request, string? protocolVersion)
    {
        ApplyHints(result, request, protocolVersion, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Marks a successful read in the embedded skill namespace as fresh for five minutes.
    /// </summary>
    /// <param name="result">The completed resource read.</param>
    /// <param name="request">The requested resource and any continuation parameters.</param>
    /// <param name="protocolVersion">The protocol version selected for the session.</param>
    public static void ApplySkillHints(ReadResourceResult result, ReadResourceRequestParams? request, string? protocolVersion)
    {
        if (request?.Uri.StartsWith("skill://roslyn-workbench/", StringComparison.Ordinal) == true)
        {
            ApplyHints(result, request, protocolVersion, TimeSpan.FromMinutes(5));
        }
    }

    private static void ApplyHints(ICacheableResult result, RequestParams? request, string? protocolVersion, TimeSpan freshness)
    {
        if (string.CompareOrdinal(protocolVersion, "2026-07-28") < 0)
        {
            return;
        }

        var isContinuation = request?.InputResponses is not null || request?.RequestState is not null;
        result.TimeToLive = isContinuation ? TimeSpan.Zero : freshness;
        result.CacheScope = CacheScope.Private;
    }
}
