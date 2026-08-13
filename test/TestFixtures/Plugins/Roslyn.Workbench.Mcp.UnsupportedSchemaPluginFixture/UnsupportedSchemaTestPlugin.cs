using System.Text.Json;
using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.UnsupportedSchemaPluginFixture;

[RoslynPlugin("test.unsupported.schema", "Unsupported Schema Test Plugin", PluginApiVersions.V1)]
public sealed class UnsupportedSchemaTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.AddQueryTool<UnsupportedRequestHandler>();
        configuration.AddQueryTool<UnsupportedResponseHandler>();
    }

    [JsonConverter(typeof(UnsupportedRequestConverter))]
    public sealed record UnsupportedRequest : WorkspaceBoundRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed record SupportedResponse : IQueryResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed record SupportedRequest : WorkspaceBoundRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    [JsonConverter(typeof(UnsupportedResponseConverter))]
    public sealed record UnsupportedResponse : IQueryResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed class UnsupportedRequestConverter : JsonConverter<UnsupportedRequest>
    {
        public UnsupportedRequestConverter()
        {
            throw new InvalidOperationException("Sensitive request converter failure.");
        }

        public override UnsupportedRequest? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            UnsupportedRequest value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class UnsupportedResponseConverter : JsonConverter<UnsupportedResponse>
    {
        public UnsupportedResponseConverter()
        {
            throw new InvalidOperationException("Sensitive response converter failure.");
        }

        public override UnsupportedResponse? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            UnsupportedResponse value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }

    [RoslynTool(
        "unsupported-request-schema",
        "Unsupported Request Schema",
        "Exposes a request contract that cannot be represented by the transport schema.")]
    private sealed class UnsupportedRequestHandler : IQueryToolHandler<UnsupportedRequest, SupportedResponse>
    {
        public ValueTask<PluginExecutionResult<SupportedResponse>> ExecuteAsync(
            UnsupportedRequest request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new SupportedResponse();
            var result = PluginExecutionResult.Success(response);

            return ValueTask.FromResult(result);
        }
    }

    [RoslynTool(
        "unsupported-response-schema",
        "Unsupported Response Schema",
        "Exposes a response contract that cannot be represented by the transport schema.")]
    private sealed class UnsupportedResponseHandler : IQueryToolHandler<SupportedRequest, UnsupportedResponse>
    {
        public ValueTask<PluginExecutionResult<UnsupportedResponse>> ExecuteAsync(
            SupportedRequest request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new UnsupportedResponse();
            var result = PluginExecutionResult.Success(response);

            return ValueTask.FromResult(result);
        }
    }
}
