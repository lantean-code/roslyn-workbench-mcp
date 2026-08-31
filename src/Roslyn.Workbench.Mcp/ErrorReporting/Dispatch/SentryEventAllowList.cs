using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Removes all Sentry event fields except the explicitly approved diagnostic payload and envelope metadata.
/// </summary>
internal static class SentryEventAllowList
{
    private const string _workbenchContext = "roslyn_workbench";
    private const string _sdkName = "sentry.dotnet";

    private static readonly string _sdkVersion =
        typeof(SentryClient).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    private static readonly ImmutableArray<string> _allowedTopLevelProperties =
    [
        "event_id",
        "timestamp",
        "platform",
        "level",
        "logger",
        "fingerprint",
        "logentry",
        "exception",
    ];

    /// <summary>
    /// Creates a new event containing only approved top-level fields and the Roslyn Workbench context.
    /// </summary>
    /// <param name="source">The event to filter before preview or submission.</param>
    /// <returns>An allow-listed copy that is safe for the configured Sentry client to process.</returns>
    public static SentryEvent CreateAllowedCopy(SentryEvent source)
    {
        var sourceBytes = SentryEventJsonSerializer.Serialize(source);
        using var sourceDocument = JsonDocument.Parse(sourceBytes.AsMemory());
        var sourceRoot = sourceDocument.RootElement;
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();

        foreach (var propertyName in _allowedTopLevelProperties)
        {
            if (sourceRoot.TryGetProperty(propertyName, out var property))
            {
                writer.WritePropertyName(propertyName);
                property.WriteTo(writer);
            }
        }

        if (sourceRoot.TryGetProperty("contexts", out var contexts)
            && contexts.TryGetProperty(_workbenchContext, out var workbenchContext))
        {
            writer.WritePropertyName("contexts");
            writer.WriteStartObject();
            writer.WritePropertyName(_workbenchContext);
            workbenchContext.WriteTo(writer);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();
        var allowedBytes = buffer.WrittenSpan.ToArray().ToImmutableArray();
        var allowedEvent = SentryEventJsonSerializer.Deserialize(allowedBytes);
        allowedEvent.Sdk.Name = _sdkName;
        allowedEvent.Sdk.Version = _sdkVersion;
        return allowedEvent;
    }
}
