using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using Roslyn.Workbench.Mcp.Workspace.Selectors;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

[Trait("Category", "Contract")]
public sealed class InputContractSchemaTransformerTests
{
    private readonly McpSdkSchemaProvider _target = new();

    [Fact]
    public void GIVEN_CrossMemberSelectors_WHEN_PublishingSchema_THEN_ShouldPublishPortableGuidanceAndNonNullValues()
    {
        var schema = _target.GetInputSchema<SelectorSchemaRequest>();
        var workspace = ResolvePropertySchema(schema, "workspace");
        var location = ResolvePropertySchema(schema, "location");

        GetPropertyNames(workspace).Should().BeEquivalentTo("alias", "path", "workspaceId");
        GetPropertyNames(location).Should().BeEquivalentTo("selection", "span");
        workspace.GetProperty("description").GetString().Should().Be("Provide workspaceId, alias, path, or any combination.");
        location.GetProperty("description").GetString().Should().Be("Provide exactly one of span or selection.");
        workspace.TryGetProperty("minProperties", out _).Should().BeFalse();
        workspace.TryGetProperty("anyOf", out _).Should().BeFalse();
        workspace.GetProperty("properties").GetProperty("alias").TryGetProperty("pattern", out _).Should().BeFalse();
        AllowsNull(workspace.GetProperty("properties").GetProperty("workspaceId")).Should().BeFalse();
        AllowsNull(location.GetProperty("properties").GetProperty("selection")).Should().BeFalse();
        workspace.TryGetProperty("oneOf", out _).Should().BeFalse();
        location.TryGetProperty("anyOf", out _).Should().BeFalse();
        location.TryGetProperty("oneOf", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ScopeSelector_WHEN_PublishingSchema_THEN_ShouldRetainCompleteGuidanceObject()
    {
        var schema = _target.GetInputSchema<SelectorSchemaRequest>();
        var scope = ResolvePropertySchema(schema, "scope");
        var properties = scope.GetProperty("properties");

        GetPropertyNames(scope).Should().BeEquivalentTo("document", "kind", "project", "projects");
        properties.GetProperty("kind").GetProperty("default").GetString().Should().Be("Solution");
        scope.TryGetProperty("anyOf", out _).Should().BeFalse();
        scope.TryGetProperty("oneOf", out _).Should().BeFalse();
        scope.TryGetProperty("if", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DefaultAndNullabilityMetadata_WHEN_PublishingSchema_THEN_ShouldPublishPropertyLocalMetadata()
    {
        var schema = _target.GetInputSchema<MetadataSchemaRequest>();
        var properties = schema.GetProperty("properties");
        var nested = ResolveSchema(schema, properties.GetProperty("nested"));

        properties.GetProperty("limit").GetProperty("default").GetInt32().Should().Be(25);
        nested.GetProperty("properties").GetProperty("name").GetProperty("default").GetString().Should().Be("NestedDefault");
        AllowsNull(properties.GetProperty("nonNullable")).Should().BeFalse();
        AllowsNull(properties.GetProperty("nullable")).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_SnapshotPreconditionProperty_WHEN_PublishingInputSchema_THEN_ShouldPublishCentralDescription()
    {
        var schema = _target.GetInputSchema<SnapshotSchemaRequest>();

        schema.GetProperty("properties").GetProperty("expectedSnapshot").GetProperty("description").GetString()
            .Should()
            .Be("Echo the snapshot returned with the data or transaction state used to construct this request.");
    }

    [Fact]
    public void GIVEN_NonEmptyGuidAttribute_WHEN_PublishingSchema_THEN_ShouldRetainSdkUuidShape()
    {
        var schema = _target.GetInputSchema<SelectorSchemaRequest>();
        var workspace = ResolvePropertySchema(schema, "workspace");
        var workspaceId = workspace.GetProperty("properties").GetProperty("workspaceId");

        workspaceId.GetProperty("format").GetString().Should().Be("uuid");
        workspaceId.TryGetProperty("not", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_SerializedPropertyName_WHEN_ValidatingCrossMemberAttribute_THEN_ShouldRetainSerializedName()
    {
        var schema = _target.GetInputSchema<SerializedNameSchemaRequest>();
        var properties = schema.GetProperty("properties");

        properties.TryGetProperty("first-value", out _).Should().BeTrue();
        properties.TryGetProperty("first", out _).Should().BeFalse();
        schema.GetProperty("description").GetString().Should().Be("Provide exactly one of first-value or second.");
        schema.TryGetProperty("oneOf", out _).Should().BeFalse();
        AllowsNull(properties.GetProperty("first-value")).Should().BeFalse();
        properties.GetProperty("first-value").TryGetProperty("pattern", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ExactlyOneCollectionRule_WHEN_PublishingSchema_THEN_ShouldPublishPresenceWithoutChangingRuntimeEmptinessRules()
    {
        var schema = _target.GetInputSchema<CollectionPresenceSchemaRequest>();
        var properties = schema.GetProperty("properties");

        schema.GetProperty("description").GetString().Should().Be("Provide exactly one of items or values.");
        schema.TryGetProperty("oneOf", out _).Should().BeFalse();
        AllowsNull(properties.GetProperty("items")).Should().BeFalse();
        AllowsNull(properties.GetProperty("values")).Should().BeFalse();
        properties.GetProperty("items").TryGetProperty("minItems", out _).Should().BeFalse();
        properties.GetProperty("values").TryGetProperty("minProperties", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ValidConditionalAttributes_WHEN_PublishingSchema_THEN_ShouldRetainSdkObjectShape()
    {
        var schema = _target.GetInputSchema<ConditionalSchemaRequest>();

        GetPropertyNames(schema).Should().BeEquivalentTo("kind", "nullableKind", "otherValue", "value");
        schema.TryGetProperty("anyOf", out _).Should().BeFalse();
        schema.TryGetProperty("oneOf", out _).Should().BeFalse();
        schema.TryGetProperty("if", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(UnknownAtLeastOneMemberSchemaRequest))]
    [InlineData(typeof(UnknownExactlyOneMemberSchemaRequest))]
    [InlineData(typeof(UnknownRequiredWhenMemberSchemaRequest))]
    [InlineData(typeof(UnknownProhibitedUnlessMemberSchemaRequest))]
    public void GIVEN_UnknownValidationMember_WHEN_PublishingSchema_THEN_ShouldRejectConfiguration(Type requestType)
    {
        var action = () => _target.GetInputSchemaForType(requestType);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*is not included in the JSON contract*");
    }

    [Theory]
    [InlineData(typeof(IncompatibleRequiredWhenValueSchemaRequest))]
    [InlineData(typeof(IncompatibleProhibitedUnlessValueSchemaRequest))]
    public void GIVEN_IncompatibleConditionalValue_WHEN_PublishingSchema_THEN_ShouldRejectConfiguration(Type requestType)
    {
        var action = () => _target.GetInputSchemaForType(requestType);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*is not compatible*");
    }

    [Fact]
    public void GIVEN_AttributedOutputContract_WHEN_PublishingValueSchema_THEN_ShouldNotApplyInputMetadata()
    {
        var schema = _target.GetValueSchema<AttributedOutputContract>();

        schema.GetProperty("description").GetString().Should().Be("Attributed output contract.");
        schema.GetProperty("properties").GetProperty("value").TryGetProperty("default", out _).Should().BeFalse();
        AllowsNull(schema.GetProperty("properties").GetProperty("value")).Should().BeTrue();
        schema.TryGetProperty("anyOf", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NonObjectSchemaNode_WHEN_TransformingSchema_THEN_ShouldLeaveNodeUnchanged()
    {
        var serializerOptions = CreateSerializerOptions();
        var createOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = static (context, _) => InputContractSchemaTransformer.Transform(context, JsonValue.Create("schema")),
        };

        var schema = AIJsonUtilities.CreateJsonSchema(
            typeof(string),
            description: null,
            hasDefaultValue: false,
            defaultValue: null,
            serializerOptions,
            createOptions);

        schema.GetString().Should().Be("schema");
    }

    [Fact]
    public void GIVEN_PropertyWithoutAttributeProvider_WHEN_TransformingSchema_THEN_ShouldRetainSdkSchema()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            foreach (var property in typeInfo.Properties)
            {
                property.AttributeProvider = null;
            }
        });

        var serializerOptions = CreateSerializerOptions(resolver);
        var createOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = InputContractSchemaTransformer.Transform,
        };

        var schema = AIJsonUtilities.CreateJsonSchema(
            typeof(UnattributedSchemaRequest),
            description: null,
            hasDefaultValue: false,
            defaultValue: null,
            serializerOptions,
            createOptions);

        schema.GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_SdkSchemaWithoutProperties_WHEN_TransformingSchema_THEN_ShouldRetainObjectSchema()
    {
        var serializerOptions = CreateSerializerOptions();
        var createOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = static (context, node) =>
            {
                if (context.TypeInfo.Type == typeof(MetadataSchemaRequest) && node is JsonObject schema)
                {
                    schema.Remove("properties");
                }

                return InputContractSchemaTransformer.Transform(context, node);
            },
        };

        var schema = AIJsonUtilities.CreateJsonSchema(
            typeof(MetadataSchemaRequest),
            description: null,
            hasDefaultValue: false,
            defaultValue: null,
            serializerOptions,
            createOptions);

        schema.TryGetProperty("properties", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("multiple", JsonValueKind.Array)]
    [InlineData("nullNode", JsonValueKind.Array)]
    public void GIVEN_UnusualNonNullableTypeArray_WHEN_TransformingSchema_THEN_ShouldRetainRemainingTypes(string mode, JsonValueKind expectedKind)
    {
        var serializerOptions = CreateSerializerOptions();
        var createOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = (context, node) =>
            {
                if (context.TypeInfo.Type == typeof(UnattributedSchemaRequest)
                    && node is JsonObject schema
                    && schema["properties"] is JsonObject properties
                    && properties["value"] is JsonObject value)
                {
                    value["type"] = mode == "multiple"
                        ? new JsonArray("string", "integer", "null")
                        : new JsonArray(null, "null");
                }

                return InputContractSchemaTransformer.Transform(context, node);
            },
        };

        var schema = AIJsonUtilities.CreateJsonSchema(
            typeof(UnattributedSchemaRequest),
            description: null,
            hasDefaultValue: false,
            defaultValue: null,
            serializerOptions,
            createOptions);

        schema.GetProperty("properties").GetProperty("value").GetProperty("type").ValueKind.Should().Be(expectedKind);
    }

    private static JsonSerializerOptions CreateSerializerOptions(IJsonTypeInfoResolver? resolver = null)
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true,
            TypeInfoResolver = resolver ?? new DefaultJsonTypeInfoResolver(),
        };
    }

    private static JsonElement ResolvePropertySchema(JsonElement root, string propertyName)
    {
        var property = root.GetProperty("properties").GetProperty(propertyName);
        return ResolveSchema(root, property);
    }

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var referenceElement))
        {
            return schema;
        }

        var reference = referenceElement.GetString();
        if (reference is null || !reference.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The test schema reference was not a local JSON Pointer.");
        }

        var current = root;
        foreach (var encodedToken in reference[2..].Split('/'))
        {
            var token = encodedToken
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            current = current.GetProperty(token);
        }

        return current;
    }

    private static string[] GetPropertyNames(JsonElement schema)
    {
        return schema.GetProperty("properties")
            .EnumerateObject()
            .Select(static property => property.Name)
            .ToArray();
    }

    private static bool AllowsNull(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return string.Equals(type.GetString(), "null", StringComparison.Ordinal);
        }

        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(static item => string.Equals(item.GetString(), "null", StringComparison.Ordinal));
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record SelectorSchemaRequest
    {
        public WorkspaceSelector? Workspace { get; init; }

        public LocationSelector? Location { get; init; }

        public ScopeSelector? Scope { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record MetadataSchemaRequest
    {
        [DefaultValue(25)]
        public int? Limit { get; init; } = 25;

        public string NonNullable { get; init; } = string.Empty;

        public string? Nullable { get; init; }

        public MetadataNestedContract? Nested { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record MetadataNestedContract
    {
        [DefaultValue("NestedDefault")]
        public string? Name { get; init; } = "NestedDefault";
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record SnapshotSchemaRequest
    {
        [Description("A deliberately conflicting property description.")]
        public SnapshotPrecondition? ExpectedSnapshot { get; init; }
    }

    [Description("Provide exactly one of first-value or second.")]
    [RequiresExactlyOne(nameof(First), nameof(Second))]
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record SerializedNameSchemaRequest
    {
        [JsonPropertyName("first-value")]
        public string? First { get; init; }

        public string? Second { get; init; }
    }

    [Description("Provide exactly one of items or values.")]
    [RequiresExactlyOne(nameof(Items), nameof(Values))]
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record CollectionPresenceSchemaRequest
    {
        public IReadOnlyList<string>? Items { get; init; }

        public IReadOnlyDictionary<string, string>? Values { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record ConditionalSchemaRequest
    {
        public ConditionalKind Kind { get; init; }

        public int? NullableKind { get; init; }

        [RequiredWhen(nameof(NullableKind), 1)]
        public string? OtherValue { get; init; }

        [RequiredWhen(nameof(Kind), ConditionalKind.Other)]
        [ProhibitedUnless(nameof(Kind), ConditionalKind.Other)]
        public string? Value { get; init; }
    }

    private enum ConditionalKind
    {
        Default,
        Other,
    }

    [RequiresAtLeastOne("Unknown")]
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record UnknownAtLeastOneMemberSchemaRequest;

    [RequiresExactlyOne(nameof(Value), "Unknown")]
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record UnknownExactlyOneMemberSchemaRequest
    {
        public string? Value { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record UnknownRequiredWhenMemberSchemaRequest
    {
        [RequiredWhen("Unknown", ConditionalKind.Other)]
        public string? Value { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record UnknownProhibitedUnlessMemberSchemaRequest
    {
        [ProhibitedUnless("Unknown", ConditionalKind.Other)]
        public string? Value { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record IncompatibleRequiredWhenValueSchemaRequest
    {
        public int Kind { get; init; }

        [RequiredWhen(nameof(Kind), "Invalid")]
        public string? Value { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record IncompatibleProhibitedUnlessValueSchemaRequest
    {
        public int Kind { get; init; }

        [ProhibitedUnless(nameof(Kind), "Invalid")]
        public string? Value { get; init; }
    }

    [RequiresAtLeastOne(nameof(Value))]
    [Description("Attributed output contract.")]
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record AttributedOutputContract
    {
        [DefaultValue("Default")]
        public string? Value { get; init; } = "Default";
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The MCP schema exporter consumes this contract through type metadata.")]
    private sealed record UnattributedSchemaRequest
    {
        public string Value { get; init; } = string.Empty;
    }
}
