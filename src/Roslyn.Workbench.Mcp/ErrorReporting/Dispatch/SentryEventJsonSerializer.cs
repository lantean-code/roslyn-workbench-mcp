using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal static class SentryEventJsonSerializer
{
    public static ImmutableArray<byte> Serialize(SentryEvent sentryEvent)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        sentryEvent.WriteTo(writer, null);
        writer.Flush();
        return buffer.WrittenSpan.ToArray().ToImmutableArray();
    }

    public static SentryEvent Deserialize(ImmutableArray<byte> bytes)
    {
        using var document = JsonDocument.Parse(bytes.AsMemory());
        return SentryEvent.FromJson(document.RootElement);
    }
}
