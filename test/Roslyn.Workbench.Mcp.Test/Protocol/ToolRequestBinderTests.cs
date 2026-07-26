using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Conversions;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

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
    public void GIVEN_BuiltInMutationRequiredArgumentsAreMissing_WHEN_Binding_THEN_ShouldReturnNamedErrors()
    {
        AssertMissingRequiredArguments<FormatDocumentRequest>("Missing required tool arguments: 'document', 'expectedSnapshot'.");
        AssertMissingRequiredArguments<OrganizeImportsRequest>("Missing required tool arguments: 'document', 'expectedSnapshot'.");
        AssertMissingRequiredArguments<RenameSymbolRequest>("Missing required tool arguments: 'expectedSnapshot', 'newName', 'symbol'.");
        AssertMissingRequiredArguments<StageCodeActionRequest>("Missing required tool arguments: 'actionId', 'expectedSnapshot'.");
        AssertMissingRequiredArguments<StageCodeFixRequest>("Missing required tool arguments: 'actionId', 'expectedSnapshot'.");
        AssertMissingRequiredArguments<StageFixAllRequest>("Missing required tool arguments: 'actionId', 'expectedSnapshot', 'scope'.");
        AssertMissingRequiredArguments<TransactionHistoryRequest>("Missing required tool arguments: 'direction', 'expectedSnapshot'.");
    }

    [Fact]
    public void GIVEN_BuiltInEnumArgumentIsUndefined_WHEN_Binding_THEN_ShouldReturnNamedError()
    {
        var selectionArguments = new Dictionary<string, JsonElement>
        {
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition()),
            ["selection"] = JsonSerializer.SerializeToElement(new LocationSelector()),
        };

        AssertUndefinedEnumArgument<AddAwaitRequest>("kind", selectionArguments);
        AssertUndefinedEnumArgument<ConvertAnonymousTypeToClassRequest>("kind", selectionArguments);
        AssertUndefinedEnumArgument<ConvertForeachLinqRequest>("conversionKind", selectionArguments);
        AssertUndefinedEnumArgument<ConvertIfToSwitchRequest>("kind", selectionArguments);
        AssertUndefinedEnumArgument<ConvertPropertyRequest>("direction", selectionArguments);
        AssertUndefinedEnumArgument<ExtractMethodRequest>("targetKind", selectionArguments);
        AssertUndefinedEnumArgument<IntroduceParameterRequest>("strategy", selectionArguments);
        AssertUndefinedEnumArgument<IntroduceVariableRequest>("kind", selectionArguments);
        AssertUndefinedEnumArgument<ServerStatusRequest>("detail", new Dictionary<string, JsonElement>());
        AssertUndefinedEnumArgument<TransactionHistoryRequest>("direction", new Dictionary<string, JsonElement>
        {
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition()),
        });
    }

    [Fact]
    public void GIVEN_CodeActionEnumArgumentIsMissing_WHEN_Binding_THEN_ShouldReturnNamedError()
    {
        var selectionArguments = new Dictionary<string, JsonElement>
        {
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition()),
            ["selection"] = JsonSerializer.SerializeToElement(new LocationSelector()),
        };

        AssertMissingRequiredArguments<AddAwaitRequest>(
            selectionArguments,
            "Missing required tool argument: 'kind'.");

        AssertMissingRequiredArguments<ConvertAnonymousTypeToClassRequest>(
            selectionArguments,
            "Missing required tool argument: 'kind'.");

        AssertMissingRequiredArguments<ConvertForeachLinqRequest>(
            selectionArguments,
            "Missing required tool argument: 'conversionKind'.");

        AssertMissingRequiredArguments<ConvertIfToSwitchRequest>(
            selectionArguments,
            "Missing required tool argument: 'kind'.");

        AssertMissingRequiredArguments<ConvertPropertyRequest>(
            selectionArguments,
            "Missing required tool argument: 'direction'.");

        AssertMissingRequiredArguments<ExtractMethodRequest>(
            selectionArguments,
            "Missing required tool argument: 'targetKind'.");

        AssertMissingRequiredArguments<IntroduceParameterRequest>(
            selectionArguments,
            "Missing required tool argument: 'strategy'.");

        AssertMissingRequiredArguments<IntroduceVariableRequest>(
            selectionArguments,
            "Missing required tool argument: 'kind'.");
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

    private static void AssertMissingRequiredArguments<TRequest>(string expectedErrorMessage)
        where TRequest : class
    {
        AssertMissingRequiredArguments<TRequest>(
            new Dictionary<string, JsonElement>(),
            expectedErrorMessage);
    }

    private static void AssertMissingRequiredArguments<TRequest>(
        IDictionary<string, JsonElement> arguments,
        string expectedErrorMessage)
        where TRequest : class
    {
        var result = ToolRequestBinder.TryBind<TRequest>(arguments, out var request, out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be(expectedErrorMessage);
    }

    private static void AssertUndefinedEnumArgument<TRequest>(
        string argumentName,
        IDictionary<string, JsonElement> validArguments)
        where TRequest : class
    {
        var arguments = new Dictionary<string, JsonElement>(validArguments)
        {
            [argumentName] = JsonSerializer.SerializeToElement(999),
        };

        var result = ToolRequestBinder.TryBind<TRequest>(
            arguments,
            out var request,
            out var errorMessage);

        result.Should().BeFalse();
        request.Should().BeNull();
        errorMessage.Should().Be($"Unsupported value for tool argument: '{argumentName}'.");
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
