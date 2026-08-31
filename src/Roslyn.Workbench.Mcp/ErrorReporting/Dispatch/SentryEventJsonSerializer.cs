using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Serializes and reconstructs Sentry events using the SDK's protocol representation.
/// </summary>
internal static class SentryEventJsonSerializer
{
    /// <summary>
    /// Serializes a Sentry event into its UTF-8 JSON representation.
    /// </summary>
    /// <param name="sentryEvent">The Sentry event to serialize as UTF-8 JSON.</param>
    /// <returns>The serialized UTF-8 JSON bytes.</returns>
    public static ImmutableArray<byte> Serialize(SentryEvent sentryEvent)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        sentryEvent.WriteTo(writer, null);
        writer.Flush();
        return buffer.WrittenSpan.ToArray().ToImmutableArray();
    }

    /// <summary>
    /// Deserializes an allow-listed Sentry event payload.
    /// </summary>
    /// <param name="bytes">The UTF-8 JSON bytes to deserialize into an allow-listed Sentry event.</param>
    /// <returns>The Sentry event reconstructed from the allow-listed JSON.</returns>
    public static SentryEvent Deserialize(ImmutableArray<byte> bytes)
    {
        using var document = JsonDocument.Parse(bytes.AsMemory());
        return SentryEvent.FromJson(document.RootElement);
    }
}
