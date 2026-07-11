using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class ToolRequestBinderTests
{
    [Fact]
    public void GIVEN_Arguments_WHEN_Binding_THEN_ShouldDeserializeWebNamedProperties()
    {
        var result = ToolRequestBinder.Deserialize<TestRequest>(new Dictionary<string, JsonElement>
        {
            ["value"] = JsonSerializer.SerializeToElement("Value"),
        });

        result.Value.Should().Be("Value");
    }

    [Fact]
    public void GIVEN_ConverterReturnsNull_WHEN_Binding_THEN_ShouldThrowJsonException()
    {
        var action = () => ToolRequestBinder.Deserialize<NullRequest>(new Dictionary<string, JsonElement>());

        action.Should().Throw<JsonException>();
    }

    public sealed record TestRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    [JsonConverter(typeof(NullRequestConverter))]
    public sealed record NullRequest
    {
    }

    public sealed class NullRequestConverter : JsonConverter<NullRequest>
    {
        public override NullRequest? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return null;
        }

        public override void Write(
            Utf8JsonWriter writer,
            NullRequest value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
    }
}
