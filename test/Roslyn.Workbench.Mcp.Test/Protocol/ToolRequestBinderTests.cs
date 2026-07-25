using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class ToolRequestBinderTests
{
    [Fact]
    public void GIVEN_Arguments_WHEN_Binding_THEN_ShouldDeserializeWebNamedProperties()
    {
        var result = ToolRequestBinder.TryBind<TestRequest>(
            new Dictionary<string, JsonElement>
            {
                ["value"] = JsonSerializer.SerializeToElement("Value"),
            },
            out var request,
            out var errorMessage);

        result.Should().BeTrue();
        request.Should().NotBeNull();
        request.Value.Should().Be("Value");
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ConverterReturnsNull_WHEN_Binding_THEN_ShouldReturnContractError()
    {
        var result = ToolRequestBinder.TryBind<NullRequest>(
            new Dictionary<string, JsonElement>(),
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().StartWith("The tool arguments did not match the request contract.");
    }

    [Fact]
    public void GIVEN_ExplicitNullForNonNullableProperty_WHEN_Binding_THEN_ShouldReturnContractError()
    {
        var result = ToolRequestBinder.TryBind<TestRequest>(
            new Dictionary<string, JsonElement>
            {
                ["value"] = JsonSerializer.SerializeToElement((string?)null),
            },
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().StartWith("The tool arguments did not match the request contract.");
    }

    [Fact]
    public void GIVEN_OneRequiredPropertyIsMissing_WHEN_ValidatingRequiredArguments_THEN_ShouldReturnNamedError()
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement("Scope"),
        };

        var result = ToolRequestBinder.TryBind<RequiredRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Missing required tool argument: 'name'.");
    }

    [Fact]
    public void GIVEN_MultipleRequiredPropertiesAreMissing_WHEN_ValidatingRequiredArguments_THEN_ShouldReturnOrderedNamedError()
    {
        var result = ToolRequestBinder.TryBind<RequiredRequest>(
            new Dictionary<string, JsonElement>(),
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Missing required tool arguments: 'name', 'scope'.");
    }

    [Fact]
    public void GIVEN_RequiredPropertiesUseDifferentCasing_WHEN_ValidatingRequiredArguments_THEN_ShouldAcceptArguments()
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["Name"] = JsonSerializer.SerializeToElement("Name"),
            ["Scope"] = JsonSerializer.SerializeToElement("Scope"),
        };

        var result = ToolRequestBinder.TryBind<RequiredRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeTrue();
        request.Should().NotBeNull();
        request.Name.Should().Be("Name");
        request.Scope.Should().Be("Scope");
        errorMessage.Should().BeNull();
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record TestRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    [JsonConverter(typeof(NullRequestConverter))]
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record NullRequest
    {
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record RequiredRequest
    {
        public required string Name { get; init; }

        public required string Scope { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the converter declared by JsonConverterAttribute.")]
    private sealed class NullRequestConverter : JsonConverter<NullRequest>
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
