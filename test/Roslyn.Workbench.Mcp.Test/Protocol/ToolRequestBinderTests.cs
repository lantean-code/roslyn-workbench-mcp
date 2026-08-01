using System.ComponentModel.DataAnnotations;
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

    [Fact]
    public void GIVEN_RequiredEnumArgumentIsUndefined_WHEN_Binding_THEN_ShouldReturnNamedError()
    {
        var result = ToolRequestBinder.TryBind<RequiredEnumRequest>(
            new Dictionary<string, JsonElement>
            {
                ["value"] = JsonSerializer.SerializeToElement(999),
            },
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Unsupported value for tool argument: 'value'.");
    }

    [Fact]
    public void GIVEN_EnumArgumentIsValidNullOrOmitted_WHEN_Binding_THEN_ShouldBindRequest()
    {
        AssertEnumRequestBinds(new Dictionary<string, JsonElement>
        {
            ["signedFlags"] = JsonSerializer.SerializeToElement(TestSignedFlags.First | TestSignedFlags.Second),
            ["unsignedFlags"] = JsonSerializer.SerializeToElement(TestUnsignedFlags.First | TestUnsignedFlags.Second),
            ["optional"] = JsonSerializer.SerializeToElement((TestEnum?)null),
        });

        AssertEnumRequestBinds(new Dictionary<string, JsonElement>());
    }

    [Fact]
    public void GIVEN_FlagsArgumentContainsUndefinedBits_WHEN_Binding_THEN_ShouldReturnNamedError()
    {
        var result = ToolRequestBinder.TryBind<EnumRequest>(
            new Dictionary<string, JsonElement>
            {
                ["signedFlags"] = JsonSerializer.SerializeToElement((TestSignedFlags)4),
                ["unsignedFlags"] = JsonSerializer.SerializeToElement((TestUnsignedFlags)4),
                ["optional"] = JsonSerializer.SerializeToElement((TestEnum)999),
            },
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Unsupported values for tool arguments: 'optional', 'signedFlags', 'unsignedFlags'.");
    }

    [Fact]
    public void GIVEN_NegativeRangedArgument_WHEN_Binding_THEN_ShouldReturnNamedError()
    {
        var result = ToolRequestBinder.TryBind<RangedRequest>(
            new Dictionary<string, JsonElement>
            {
                ["limit"] = JsonSerializer.SerializeToElement(-1),
            },
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Invalid value for tool argument: 'limit'.");
    }

    [Fact]
    public void GIVEN_MultipleInvalidRangedArguments_WHEN_Binding_THEN_ShouldReturnOrderedNamedError()
    {
        var result = ToolRequestBinder.TryBind<MultipleRangedRequest>(
            new Dictionary<string, JsonElement>
            {
                ["secondLimit"] = JsonSerializer.SerializeToElement(-1),
                ["firstLimit"] = JsonSerializer.SerializeToElement(-1),
            },
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Invalid values for tool arguments: 'firstLimit', 'secondLimit'.");
    }

    [Fact]
    public void GIVEN_RequiredStringIsWhitespace_WHEN_Binding_THEN_ShouldReturnNamedValueError()
    {
        var result = ToolRequestBinder.TryBind<MeaningfulStringRequest>(
            new Dictionary<string, JsonElement>
            {
                ["value"] = JsonSerializer.SerializeToElement(" "),
            },
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Invalid value for tool argument: 'value'.");
    }

    [Fact]
    public void GIVEN_DataAnnotatedRequiredStringIsOmitted_WHEN_Binding_THEN_ShouldReturnNamedMissingArgumentError()
    {
        var result = ToolRequestBinder.TryBind<DataAnnotatedRequiredRequest>(
            new Dictionary<string, JsonElement>(),
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Missing required tool argument: 'value'.");
    }

    [Fact]
    public void GIVEN_StringIsNotAnAllowedValue_WHEN_Binding_THEN_ShouldReturnNamedValueError()
    {
        var result = ToolRequestBinder.TryBind<AllowedValueRequest>(
            new Dictionary<string, JsonElement>
            {
                ["value"] = JsonSerializer.SerializeToElement("Third"),
            },
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Invalid value for tool argument: 'value'.");
    }

    [Fact]
    public void GIVEN_NestedValidationAttributesFail_WHEN_Binding_THEN_ShouldReturnNestedArgumentPaths()
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["item"] = JsonSerializer.SerializeToElement(new { limit = -1 }),
            ["items"] = JsonSerializer.SerializeToElement(new[] { new { kind = 999 } }),
        };

        var result = ToolRequestBinder.TryBind<NestedValidationRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Invalid values for tool arguments: 'item.limit', 'items[0].kind'.");
    }

    [Fact]
    public void GIVEN_EmptyNestedWorkspaceId_WHEN_Binding_THEN_ShouldReturnNestedArgumentPath()
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["workspace"] = JsonSerializer.SerializeToElement(new { workspaceId = Guid.Empty }),
        };

        var result = ToolRequestBinder.TryBind<NestedWorkspaceRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be("Invalid value for tool argument: 'workspace.workspaceId'.");
    }

    [Fact]
    public void GIVEN_ValidNestedWorkspaceId_WHEN_Binding_THEN_ShouldPreserveWorkspaceId()
    {
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var arguments = new Dictionary<string, JsonElement>
        {
            ["workspace"] = JsonSerializer.SerializeToElement(new { workspaceId }),
        };

        var result = ToolRequestBinder.TryBind<NestedWorkspaceRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeTrue();
        request.Should().NotBeNull();
        request.Workspace.WorkspaceId.Should().Be(workspaceId);
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null, 25)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    public void GIVEN_ValidOptionalRangedArgument_WHEN_Binding_THEN_ShouldPreserveRequestedOrDefaultValue(
        int? requestedLimit,
        int expectedLimit)
    {
        var arguments = new Dictionary<string, JsonElement>();
        if (requestedLimit is not null)
        {
            arguments["limit"] = JsonSerializer.SerializeToElement(requestedLimit);
        }

        var result = ToolRequestBinder.TryBind<RangedRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeTrue();
        request.Should().NotBeNull();
        request.Limit.Should().Be(expectedLimit);
        errorMessage.Should().BeNull();
    }

    private static void AssertEnumRequestBinds(IDictionary<string, JsonElement> arguments)
    {
        var result = ToolRequestBinder.TryBind<EnumRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeTrue();
        request.Should().NotBeNull();
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
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record EnumRequest
    {
        public TestSignedFlags SignedFlags { get; init; }

        public TestUnsignedFlags UnsignedFlags { get; init; }

        public TestEnum? Optional { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record RequiredEnumRequest
    {
        public required TestEnum Value { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record RangedRequest
    {
        [Range(0, int.MaxValue)]
        public int? Limit { get; init; } = 25;
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record MultipleRangedRequest
    {
        [Range(0, int.MaxValue)]
        public int? FirstLimit { get; init; } = 25;

        [Range(0, int.MaxValue)]
        public int? SecondLimit { get; init; } = 25;
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record MeaningfulStringRequest
    {
        [Required]
        public required string Value { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record AllowedValueRequest
    {
        [AllowedValues("First", "Second")]
        public string Value { get; init; } = "First";
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record DataAnnotatedRequiredRequest
    {
        [Required]
        public string Value { get; init; } = "Value";
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record NestedValidationRequest
    {
        public NestedValidationValue Item { get; init; } = new();

        public IReadOnlyList<NestedValidationValue> Items { get; init; } = [];
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the value through the request deserialisation path exercised by this test.")]
    private sealed record NestedValidationValue
    {
        [Range(0, int.MaxValue)]
        public int Limit { get; init; }

        public TestEnum Kind { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates the request through the generic deserialisation path exercised by this test.")]
    private sealed record NestedWorkspaceRequest
    {
        public WorkspaceSelector Workspace { get; init; } = new();
    }

    private enum TestEnum
    {
        Value,
    }

    [Flags]
    private enum TestSignedFlags
    {
        None = 0,
        First = 1,
        Second = 2,
    }

    [Flags]
    private enum TestUnsignedFlags : uint
    {
        None = 0,
        First = 1,
        Second = 2,
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
