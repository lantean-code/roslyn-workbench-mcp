using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

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
    ];

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
            writer.WritePropertyName(propertyName);
            sourceRoot.GetProperty(propertyName).WriteTo(writer);
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
