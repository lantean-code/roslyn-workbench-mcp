using System.Buffers;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Serializes structured MCP tool result envelopes using the published response contracts.
/// </summary>
internal static class ToolResultEnvelopeSerializer
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Creates a successful envelope that publishes the response payload under the shared data property.
    /// </summary>
    /// <param name="data">The successful response payload.</param>
    /// <returns>The structured JSON payload.</returns>
    /// <typeparam name="TData">The successful response payload type.</typeparam>
    public static JsonElement CreateSuccess<TData>(TData? data)
    {
        return BuildPayload(writer =>
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", true);
            writer.WritePropertyName("data");

            if (data is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                SerializeObject(data).WriteTo(writer);
            }

            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Creates a successful envelope for a mutation result.
    /// </summary>
    /// <param name="data">The successful mutation payload.</param>
    /// <param name="staged">A value indicating whether a mutation was staged.</param>
    /// <returns>The structured JSON payload.</returns>
    public static JsonElement CreateMutationSuccess(MutationData? data, bool staged)
    {
        return BuildPayload(writer =>
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", true);
            writer.WriteStartObject("data");
            writer.WriteBoolean("staged", staged);

            if (staged && data is not null)
            {
                writer.WritePropertyName("summary");
                JsonSerializer.Serialize(writer, data.Summary, _serializerOptions);

                if (data.Transaction?.Revision is int revision)
                {
                    writer.WriteStartObject("transaction");
                    writer.WriteNumber("revision", revision);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Creates a failed envelope with the published error payload.
    /// </summary>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The optional follow-up action.</param>
    /// <param name="diagnostics">The diagnostics that explain the failure.</param>
    /// <param name="warnings">The warnings associated with the failure.</param>
    /// <returns>The structured JSON payload.</returns>
    public static JsonElement CreateFailure(
        ToolError? error,
        RequiredAction? requiredAction,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return BuildPayload(writer =>
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", false);
            writer.WritePropertyName("error");

            if (error is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                JsonSerializer.Serialize(writer, error, _serializerOptions);
            }

            if (requiredAction is not null)
            {
                writer.WritePropertyName("next");
                JsonSerializer.Serialize(writer, requiredAction, _serializerOptions);
            }

            if (diagnostics is { Count: > 0 })
            {
                writer.WritePropertyName("diagnostics");
                JsonSerializer.Serialize(writer, diagnostics, _serializerOptions);
            }

            if (warnings is { Count: > 0 })
            {
                writer.WritePropertyName("warnings");
                JsonSerializer.Serialize(writer, warnings, _serializerOptions);
            }

            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Creates a failed envelope for an unhandled tool exception.
    /// </summary>
    /// <param name="correlationId">The server-side diagnostic correlation identifier.</param>
    /// <returns>The structured JSON payload.</returns>
    public static JsonElement CreateUnhandledException(string correlationId)
    {
        return CreateFailure(
            new ToolError
            {
                Code = "UnhandledException",
                Message = "Tool execution failed.",
                CorrelationId = correlationId,
            },
            requiredAction: null);
    }

    private static JsonElement BuildPayload(Action<Utf8JsonWriter> writePayload)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writePayload(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static JsonElement SerializeObject<TData>(TData value)
    {
        var serialized = JsonSerializer.SerializeToElement(value, _serializerOptions);

        if (serialized.ValueKind == JsonValueKind.Object)
        {
            return serialized;
        }

        throw new InvalidOperationException($"Published response type '{typeof(TData).FullName}' must serialize as a JSON object.");
    }
}
