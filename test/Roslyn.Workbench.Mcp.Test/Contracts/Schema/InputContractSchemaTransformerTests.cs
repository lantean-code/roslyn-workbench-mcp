using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

[Trait("Category", "Contract")]
public sealed class InputContractSchemaTransformerTests
{
    private readonly ToolSchemaFactory _target = new(new McpSdkSchemaProvider());

    [Theory]
    [InlineData("workspace", "anyOf", 3)]
    [InlineData("project", "anyOf", 4)]
    [InlineData("document", "oneOf", 2)]
    [InlineData("location", "oneOf", 2)]
    [InlineData("symbol", "oneOf", 2)]
    public void GIVEN_WorkspaceSelectorProperty_WHEN_PublishingSchema_THEN_ShouldPublishSemanticAlternatives(
        string propertyName,
        string alternativeKeyword,
        int expectedAlternativeCount)
    {
        var schema = _target.CreateInputSchema<SelectorSchemaRequest>();

        var selectorSchema = ResolvePropertyObjectSchema(schema, propertyName);
        var constraint = selectorSchema.GetProperty("allOf")[0];

        constraint.GetProperty(alternativeKeyword).GetArrayLength().Should().Be(expectedAlternativeCount);
    }

    [Fact]
    public void GIVEN_ScopeSelector_WHEN_PublishingSchema_THEN_ShouldPublishKindSpecificRequirements()
    {
        var schema = _target.CreateInputSchema<SelectorSchemaRequest>();

        var scopeSchema = ResolvePropertyObjectSchema(schema, "scope");
        var constraints = scopeSchema.GetProperty("allOf");

        constraints.GetArrayLength().Should().Be(6);
        AssertRequiredWhenConstraint(constraints[0], "Project", "project");
        AssertProhibitedUnlessConstraint(constraints[1], "Project", "project");
        AssertRequiredWhenConstraint(constraints[2], "Document", "document");
        AssertProhibitedUnlessConstraint(constraints[3], "Document", "document");
        AssertRequiredWhenConstraint(constraints[4], "Projects", "projects");
        AssertProhibitedUnlessConstraint(constraints[5], "Projects", "projects");
        constraints[4]
            .GetProperty("then")
            .GetProperty("properties")
            .GetProperty("projects")
            .GetProperty("minItems")
            .GetInt32()
            .Should()
            .Be(1);
    }

    [Fact]
    public void GIVEN_ProjectSelectorsInDictionary_WHEN_PublishingSchema_THEN_ShouldPublishValueConstraint()
    {
        var schema = _target.CreateInputSchema<ProjectDictionarySchemaRequest>();
        var projectsSchema = ResolvePropertyObjectSchema(schema, "projects");
        var valueSchema = ResolveObjectSchema(schema, projectsSchema.GetProperty("additionalProperties"));
        var constraint = valueSchema.GetProperty("allOf")[0];

        constraint.GetProperty("anyOf").GetArrayLength().Should().Be(4);
    }

    [Theory]
    [InlineData("document", 0, "documentId")]
    [InlineData("document", 1, "path")]
    [InlineData("symbol", 0, "documentationCommentId")]
    public void GIVEN_ExclusiveStringSelector_WHEN_PublishingSchema_THEN_ShouldAllowUnusedBlankAlternative(
        string selectorName,
        int branchIndex,
        string unusedPropertyName)
    {
        var schema = _target.CreateInputSchema<SelectorSchemaRequest>();
        var selectorSchema = ResolvePropertyObjectSchema(schema, selectorName);
        var branch = selectorSchema.GetProperty("allOf")[0].GetProperty("oneOf")[branchIndex];
        var unusedProperty = branch.GetProperty("properties").GetProperty(unusedPropertyName);

        unusedProperty.GetProperty("not").GetProperty("pattern").GetString().Should().Be(@"\S");
    }

    [Fact]
    public void GIVEN_ProjectSelectorsInCollection_WHEN_PublishingSchema_THEN_ShouldPublishElementConstraint()
    {
        var schema = _target.CreateInputSchema<ProjectCollectionSchemaRequest>();
        var projectsSchema = ResolvePropertyObjectSchema(schema, "projects");
        var itemSchema = ResolveObjectSchema(schema, projectsSchema.GetProperty("items"));
        var constraint = itemSchema.GetProperty("allOf")[0];

        constraint.GetProperty("anyOf").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void GIVEN_NonEmptyGuidAttribute_WHEN_PublishingSchema_THEN_ShouldExcludeEmptyGuid()
    {
        var schema = _target.CreateInputSchema<SelectorSchemaRequest>();
        var workspaceSchema = ResolvePropertyObjectSchema(schema, "workspace");
        var constraints = workspaceSchema.GetProperty("allOf");

        constraints[1]
            .GetProperty("properties")
            .GetProperty("workspaceId")
            .GetProperty("not")
            .GetProperty("const")
            .GetGuid()
            .Should()
            .Be(Guid.Empty);
    }

    [Fact]
    public void GIVEN_PluginDefinedAttributedContract_WHEN_PublishingSchema_THEN_ShouldUseSerializedPropertyNames()
    {
        var schema = _target.CreateInputSchema<PluginSchemaRequest>();
        var selectorSchema = ResolvePropertyObjectSchema(schema, "selector");
        var branches = selectorSchema.GetProperty("allOf")[0].GetProperty("oneOf");

        branches[0].GetProperty("required")[0].GetString().Should().Be("first-value");
        branches[1].GetProperty("required")[0].GetString().Should().Be("second");
    }

    [Fact]
    public void GIVEN_IncompatibleConditionalValue_WHEN_PublishingSchema_THEN_ShouldRejectConfiguration()
    {
        var action = () => _target.CreateInputSchema<InvalidConditionalSchemaRequest>();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not compatible*");
    }

    [Fact]
    public void GIVEN_UnknownAttributedMember_WHEN_PublishingSchema_THEN_ShouldRejectConfiguration()
    {
        var action = () => _target.CreateInputSchema<UnknownMemberSchemaRequest>();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown*");
    }

    [Fact]
    public void GIVEN_DictionaryGroupMember_WHEN_PublishingSchema_THEN_ShouldRequireNonEmptyObject()
    {
        var schema = _target.CreateInputSchema<DictionaryGroupSchemaRequest>();
        var selectorSchema = ResolvePropertyObjectSchema(schema, "selector");
        var valuesConstraint = selectorSchema
            .GetProperty("allOf")[0]
            .GetProperty("anyOf")[0]
            .GetProperty("properties")
            .GetProperty("values");

        valuesConstraint.GetProperty("type").GetString().Should().Be("object");
        valuesConstraint.GetProperty("minProperties").GetInt32().Should().Be(1);
    }

    [Fact]
    public void GIVEN_NullableConditionalController_WHEN_PublishingSchema_THEN_ShouldPublishExpectedValue()
    {
        var schema = _target.CreateInputSchema<NullableConditionalSchemaRequest>();
        var requestSchema = ResolveObjectSchema(schema, schema);
        var constraint = requestSchema.GetProperty("allOf")[0];

        constraint.GetProperty("if").GetProperty("properties").GetProperty("kind").GetProperty("const").GetInt32().Should().Be(1);
    }

    [Fact]
    public void GIVEN_OmittedValueTypeConditionalController_WHEN_PublishingSchema_THEN_ShouldUseDeserializedDefault()
    {
        var schema = _target.CreateInputSchema<DefaultedConditionalControllerSchemaRequest>();
        var constraints = schema.GetProperty("allOf");
        var requiredAtDefaultCondition = constraints[0].GetProperty("if");
        var prohibitedOutsideDefaultCondition = constraints[1].GetProperty("if").GetProperty("not");
        var requiredAtOtherCondition = constraints[2].GetProperty("if");

        requiredAtDefaultCondition.TryGetProperty("required", out _).Should().BeFalse();
        prohibitedOutsideDefaultCondition.TryGetProperty("required", out _).Should().BeFalse();
        requiredAtOtherCondition.GetProperty("required")[0].GetString().Should().Be("kind");
    }

    [Fact]
    public void GIVEN_DefaultedGroupMembers_WHEN_PublishingSchema_THEN_ShouldModelValuesAfterOmission()
    {
        var schema = _target.CreateInputSchema<DefaultedGroupSchemaRequest>();
        var constraints = schema.GetProperty("allOf");
        var atLeastOneBranches = constraints[0].GetProperty("anyOf");
        var exactlyOneBranches = constraints[1].GetProperty("oneOf");

        atLeastOneBranches[0].TryGetProperty("required", out _).Should().BeFalse();
        atLeastOneBranches[1].GetProperty("required")[0].GetString().Should().Be("otherValue");
        exactlyOneBranches[0].TryGetProperty("required", out _).Should().BeFalse();
        exactlyOneBranches[1]
            .GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .BeEquivalentTo(["otherValue", "defaultValue"]);
    }

    [Fact]
    public void GIVEN_DefaultedConditionalTargets_WHEN_PublishingSchema_THEN_ShouldModelValuesAfterOmission()
    {
        var schema = _target.CreateInputSchema<DefaultedConditionalTargetSchemaRequest>();
        var constraints = schema.GetProperty("allOf");
        var requiredConsequence = constraints[0].GetProperty("then");
        var prohibitedConsequence = constraints[1].GetProperty("then");

        requiredConsequence.TryGetProperty("required", out _).Should().BeFalse();
        prohibitedConsequence.GetProperty("required")[0].GetString().Should().Be("prohibitedValue");
    }

    [Fact]
    public void GIVEN_DefaultValueAttributes_WHEN_PublishingInputSchema_THEN_ShouldPublishDefaultsThroughoutContractGraph()
    {
        var schema = _target.CreateInputSchema<DefaultedSchemaRequest>();
        var properties = schema.GetProperty("properties");
        var nestedSchema = ResolveObjectSchema(schema, properties.GetProperty("nested"));
        var itemsSchema = ResolveObjectSchema(schema, properties.GetProperty("items"));
        var itemSchema = ResolveObjectSchema(schema, itemsSchema.GetProperty("items"));
        var valuesSchema = ResolveObjectSchema(schema, properties.GetProperty("values"));
        var valueSchema = ResolveObjectSchema(schema, valuesSchema.GetProperty("additionalProperties"));

        properties.GetProperty("limit").GetProperty("default").GetInt32().Should().Be(25);
        nestedSchema.GetProperty("properties").GetProperty("name").GetProperty("default").GetString().Should().Be("NestedDefault");
        itemSchema.GetProperty("properties").GetProperty("name").GetProperty("default").GetString().Should().Be("NestedDefault");
        valueSchema.GetProperty("properties").GetProperty("count").GetProperty("default").GetInt32().Should().Be(7);
    }

    [Fact]
    public void GIVEN_NullabilityAnnotations_WHEN_PublishingInputSchema_THEN_ShouldPreserveNullabilityState()
    {
        var schema = _target.CreateInputSchema<NullabilitySchemaRequest>();
        var properties = schema.GetProperty("properties");

        AllowsNull(properties.GetProperty("nonNullable")).Should().BeFalse();
        AllowsNull(properties.GetProperty("nullable")).Should().BeTrue();
        AllowsNull(properties.GetProperty("notNullAnnotated")).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_AttributedOutputContract_WHEN_PublishingValueSchema_THEN_ShouldNotApplyInputContractMetadata()
    {
        var provider = new McpSdkSchemaProvider();

        var schema = provider.GetValueSchema<AttributedOutputContract>();

        schema.TryGetProperty("allOf", out _).Should().BeFalse();
        schema.GetProperty("properties").GetProperty("value").TryGetProperty("default", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NonObjectSchemaNode_WHEN_TransformingSchema_THEN_ShouldLeaveNodeUnchanged()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var createOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = static (context, _) =>
            {
                var replacement = JsonValue.Create("schema");
                if (replacement is null)
                {
                    throw new InvalidOperationException("The replacement schema node was not created.");
                }

                return InputContractSchemaTransformer.Transform(context, replacement);
            },
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
    public void GIVEN_PropertyWithoutAttributeProvider_WHEN_TransformingSchema_THEN_ShouldPublishBaseSchema()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            foreach (var property in typeInfo.Properties)
            {
                property.AttributeProvider = null;
            }
        });
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = resolver,
        };
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

    private static JsonElement ResolvePropertyObjectSchema(JsonElement root, string propertyName)
    {
        var propertySchema = root.GetProperty("properties").GetProperty(propertyName);
        return ResolveObjectSchema(root, propertySchema);
    }

    private static bool AllowsNull(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => string.Equals(type.GetString(), "null", StringComparison.Ordinal),
            JsonValueKind.Array => type.EnumerateArray().Any(static item => string.Equals(item.GetString(), "null", StringComparison.Ordinal)),
            _ => false,
        };
    }

    private static void AssertRequiredWhenConstraint(JsonElement constraint, string kind, string propertyName)
    {
        constraint.GetProperty("if").GetProperty("properties").GetProperty("kind").GetProperty("const").GetString().Should().Be(kind);
        constraint.GetProperty("then").GetProperty("required").EnumerateArray().Select(static item => item.GetString()).Should().Contain(propertyName);
    }

    private static void AssertProhibitedUnlessConstraint(JsonElement constraint, string kind, string propertyName)
    {
        constraint
            .GetProperty("if")
            .GetProperty("not")
            .GetProperty("properties")
            .GetProperty("kind")
            .GetProperty("const")
            .GetString()
            .Should()
            .Be(kind);
        constraint
            .GetProperty("then")
            .GetProperty("properties")
            .GetProperty(propertyName)
            .GetProperty("not")
            .Should()
            .NotBeNull();
    }

    private static JsonElement ResolveObjectSchema(JsonElement root, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            return ResolveReference(root, reference.GetString());
        }

        if (schema.TryGetProperty("anyOf", out var alternatives))
        {
            foreach (var alternative in alternatives.EnumerateArray())
            {
                if (alternative.TryGetProperty("$ref", out reference))
                {
                    return ResolveReference(root, reference.GetString());
                }

                if (alternative.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() != "null")
                {
                    return alternative;
                }
            }
        }

        return schema;
    }

    private static JsonElement ResolveReference(JsonElement root, [NotNull] string? reference)
    {
        reference.Should().StartWith("#/$defs/");
        var definitionName = reference["#/$defs/".Length..]
            .Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

        return root.GetProperty("$defs").GetProperty(definitionName);
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record SelectorSchemaRequest
    {
        public WorkspaceSelector? Workspace { get; init; }

        public ProjectSelector? Project { get; init; }

        public DocumentSelector? Document { get; init; }

        public LocationSelector? Location { get; init; }

        public SymbolSelector? Symbol { get; init; }

        public ScopeSelector? Scope { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record ProjectCollectionSchemaRequest
    {
        public IReadOnlyList<ProjectSelector>? Projects { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record ProjectDictionarySchemaRequest
    {
        public IReadOnlyDictionary<string, ProjectSelector>? Projects { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record PluginSchemaRequest
    {
        public PluginSelector? Selector { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    [RequiresExactlyOne(nameof(First), nameof(Second))]
    private sealed record PluginSelector
    {
        [JsonPropertyName("first-value")]
        public string? First { get; init; }

        public string? Second { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record InvalidConditionalSchemaRequest
    {
        public int Kind { get; init; }

        [RequiredWhen(nameof(Kind), "Invalid")]
        public string? Value { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    [RequiresAtLeastOne("Unknown")]
    private sealed record UnknownMemberSchemaRequest;

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record DictionaryGroupSchemaRequest
    {
        public DictionaryGroupSelector? Selector { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    [RequiresAtLeastOne(nameof(Values))]
    private sealed record DictionaryGroupSelector
    {
        public IReadOnlyDictionary<string, string>? Values { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record NullableConditionalSchemaRequest
    {
        public int? Kind { get; init; }

        [RequiredWhen(nameof(Kind), 1)]
        public string? Value { get; init; }
    }

    private enum ConditionalKind
    {
        Default,
        Other,
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record DefaultedConditionalControllerSchemaRequest
    {
        public ConditionalKind Kind { get; init; }

        [RequiredWhen(nameof(Kind), ConditionalKind.Default)]
        public string? RequiredAtDefault { get; init; }

        [ProhibitedUnless(nameof(Kind), ConditionalKind.Default)]
        public string? ProhibitedOutsideDefault { get; init; }

        [RequiredWhen(nameof(Kind), ConditionalKind.Other)]
        public string? RequiredAtOther { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    [RequiresAtLeastOne(nameof(DefaultValue), nameof(OtherValue))]
    [RequiresExactlyOne(nameof(DefaultValue), nameof(OtherValue))]
    private sealed record DefaultedGroupSchemaRequest
    {
        [DefaultValue("Default")]
        public string? DefaultValue { get; init; } = "Default";

        public string? OtherValue { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record DefaultedConditionalTargetSchemaRequest
    {
        public ConditionalKind Kind { get; init; }

        [DefaultValue("Default")]
        [RequiredWhen(nameof(Kind), ConditionalKind.Other)]
        public string? RequiredValue { get; init; } = "Default";

        [DefaultValue("Default")]
        [ProhibitedUnless(nameof(Kind), ConditionalKind.Other)]
        public string? ProhibitedValue { get; init; } = "Default";
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record DefaultedSchemaRequest
    {
        [DefaultValue(25)]
        public int? Limit { get; init; } = 25;

        public DefaultedNestedContract? Nested { get; init; }

        public IReadOnlyList<DefaultedNestedContract>? Items { get; init; }

        public IReadOnlyDictionary<string, DefaultedDictionaryValueContract>? Values { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record NullabilitySchemaRequest
    {
        public string NonNullable { get; init; } = string.Empty;

        public string? Nullable { get; init; }

        [NotNull]
        public string? NotNullAnnotated { get; init; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record DefaultedNestedContract
    {
        [DefaultValue("NestedDefault")]
        public string? Name { get; init; } = "NestedDefault";
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic schema path exercised by this test.")]
    private sealed record DefaultedDictionaryValueContract
    {
        [DefaultValue(7)]
        public int Count { get; init; } = 7;
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The MCP schema exporter creates the contract through the generic value-schema path exercised by this test.")]
    [RequiresAtLeastOne(nameof(Value))]
    private sealed record AttributedOutputContract
    {
        [DefaultValue("Default")]
        public string? Value { get; init; } = "Default";
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The schema exporter creates the contract through the type-based schema path exercised by this test.")]
    private sealed record UnattributedSchemaRequest
    {
        public string? Value { get; init; }
    }
}
