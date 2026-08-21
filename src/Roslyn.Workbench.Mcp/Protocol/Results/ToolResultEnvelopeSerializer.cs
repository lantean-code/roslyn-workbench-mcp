using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Serializes structured MCP tool result envelopes using the published response contracts.
/// </summary>
internal static class ToolResultEnvelopeSerializer
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// Creates a successful envelope that publishes the response payload under the shared data property.
    /// </summary>
    /// <param name="data">The successful response payload.</param>
    /// <param name="snapshot">The exact immutable workspace snapshot, when available.</param>
    /// <returns>The structured JSON payload.</returns>
    /// <typeparam name="TData">The successful response payload type.</typeparam>
    public static JsonElement CreateSuccess<TData>(
        TData? data,
        SnapshotPrecondition? snapshot = null)
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

            WriteSnapshot(writer, snapshot);

            writer.WriteEndObject();
        });
    }

    public static JsonTypeInfoKind GetSuccessDataContractKind(Type dataType)
    {
        var typeInfo = _serializerOptions.GetTypeInfo(dataType);
        return typeInfo.Kind;
    }

    /// <summary>
    /// Creates a successful envelope for a mutation result.
    /// </summary>
    /// <param name="data">The successful mutation payload.</param>
    /// <param name="staged">A value indicating whether a mutation was staged.</param>
    /// <param name="currentSnapshot">The snapshot acquired before invoking the mutation handler.</param>
    /// <returns>The structured JSON payload.</returns>
    public static JsonElement CreateMutationSuccess(
        MutationData? data,
        bool staged,
        SnapshotPrecondition currentSnapshot)
    {
        var snapshot = staged && data is not null
            ? data.Snapshot
            : currentSnapshot;

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
            WriteSnapshot(writer, snapshot);
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

            var continuation = RequiredActionContinuationMapper.Map(requiredAction);
            if (continuation is not null)
            {
                writer.WritePropertyName("continuation");
                JsonSerializer.Serialize(writer, continuation, _serializerOptions);
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
    public static JsonElement CreateUnhandledException(
        Guid correlationId,
        ErrorReportingAvailability? reporting = null)
    {
        return BuildPayload(writer =>
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", false);
            writer.WriteStartObject("error");
            writer.WriteString("code", "UnhandledException");
            writer.WriteString("message", "Tool execution failed.");
            writer.WriteString("correlationId", correlationId);
            writer.WriteEndObject();

            writer.WriteStartObject("diagnostics");
            writer.WriteBoolean("detailsAvailable", true);
            writer.WriteString("detailsTool", ServerOwnedToolRegistration.GetErrorDetailsName);
            writer.WriteEndObject();

            if (reporting is not null)
            {
                writer.WriteStartObject("reporting");
                writer.WriteString("state", reporting.State.ToString());
                writer.WriteBoolean("canPrepare", reporting.CanPrepare);
                if (reporting.PrepareTool is not null)
                {
                    writer.WriteString("prepareTool", reporting.PrepareTool);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });
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

    private static void WriteSnapshot(
        Utf8JsonWriter writer,
        SnapshotPrecondition? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        writer.WritePropertyName("snapshot");
        JsonSerializer.Serialize(writer, snapshot, _serializerOptions);
    }
}
