using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpSdkSchemaProviderIntegrationTests
{
    private readonly McpSdkSchemaProvider _target;

    public McpSdkSchemaProviderIntegrationTests()
    {
        _target = new McpSdkSchemaProvider();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_RequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishRequestProperties()
    {
        var result = _target.GetInputSchema<TestRequest>();

        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_EmptyRequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishClosedObject()
    {
        var result = _target.GetInputSchema<WorkspaceListRequest>();

        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_DataAnnotations_WHEN_ExportingInputSchema_THEN_ShouldPublishValidationKeywords()
    {
        var result = _target.GetInputSchema<AnnotatedRequest>();

        var properties = result.GetProperty("properties");
        var range = properties.GetProperty("range");
        var text = properties.GetProperty("text");
        var choice = properties.GetProperty("choice");

        range.GetProperty("minimum").GetInt32().Should().Be(1);
        range.GetProperty("maximum").GetInt32().Should().Be(10);
        text.GetProperty("minLength").GetInt32().Should().Be(2);
        text.GetProperty("maxLength").GetInt32().Should().Be(10);
        choice.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Equal("First", "Second");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_UnattributedEnum_WHEN_ExportingSchemas_THEN_ShouldPublishExactStringValues()
    {
        var inputSchema = _target.GetInputSchema<EnumRequest>();
        var valueSchema = _target.GetValueSchema<EnumResponse>();

        AssertStringEnum(
            inputSchema.GetProperty("properties").GetProperty("value"),
            "First",
            "Second");

        AssertStringEnum(
            valueSchema.GetProperty("properties").GetProperty("value"),
            "First",
            "Second");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CodeActionEnums_WHEN_ExportingSchemas_THEN_ShouldPublishExactStringValues()
    {
        var listRequestSchema = _target.GetInputSchema<ListCodeActionsRequest>();
        var prepareRequestSchema = _target.GetInputSchema<PrepareFixAllRequest>();
        var itemSchema = _target.GetValueSchema<CodeActionListItem>();
        var prepareDataSchema = _target.GetValueSchema<PrepareFixAllData>();

        AssertStringEnum(
            listRequestSchema.GetProperty("properties").GetProperty("kinds"),
            "CodeFixes",
            "Refactorings",
            "All");

        AssertStringEnum(
            prepareRequestSchema.GetProperty("properties").GetProperty("scope"),
            "Document",
            "Project",
            "Solution");

        AssertStringEnum(
            itemSchema.GetProperty("properties").GetProperty("kind"),
            "CodeFix",
            "Refactoring");

        var fixAllScopes = itemSchema.GetProperty("properties").GetProperty("fixAllScopes");
        AssertStringEnum(
            fixAllScopes.GetProperty("items"),
            "Document",
            "Project",
            "Solution");

        AssertStringEnum(
            prepareDataSchema.GetProperty("properties").GetProperty("scope"),
            "Document",
            "Project",
            "Solution");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PresenceAndNullabilityContracts_WHEN_ExportingInputSchema_THEN_ShouldPublishPresenceAndNullabilityState()
    {
        var result = _target.GetInputSchema<PresenceRequest>();

        var required = result.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();

        required.Should().Contain("dataAnnotatedRequired");
        required.Should().Contain("requiredNonNullable");
        required.Should().Contain("requiredNullable");
        required.Should().NotContain("notNullAnnotated");

        var properties = result.GetProperty("properties");
        AllowsNull(properties.GetProperty("dataAnnotatedRequired")).Should().BeTrue();
        AllowsNull(properties.GetProperty("requiredNonNullable")).Should().BeFalse();
        AllowsNull(properties.GetProperty("requiredNullable")).Should().BeTrue();
        AllowsNull(properties.GetProperty("notNullAnnotated")).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PropertyDefaultValueAttribute_WHEN_ExportingInputSchema_THEN_ShouldPublishPropertyDefault()
    {
        var result = _target.GetInputSchema<DefaultedRequest>();

        result.GetProperty("properties")
            .GetProperty("limit")
            .GetProperty("default")
            .GetInt32()
            .Should()
            .Be(25);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ReferenceHeavyRequest_WHEN_ExportingInputSchema_THEN_ShouldRebaseReferencesToPublishedRoot()
    {
        var result = _target.GetInputSchema<FindReferencesRequest>();
        var json = result.GetRawText();

        json.Should().Contain("\"$ref\":\"#/properties/");
        json.Should().NotContain("#/properties/request/");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ObjectContract_WHEN_ExportingValueSchema_THEN_ShouldPublishProperties()
    {
        var result = _target.GetValueSchema<TestResponse>();

        result.GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PrimitiveBoundedCollection_WHEN_ExportingValueSchema_THEN_ShouldPublishBoundedCollectionProperties()
    {
        var result = _target.GetValueSchema<BoundedCollection<string>>();

        result.GetProperty("properties").TryGetProperty("items", out _).Should().BeTrue();
        result.GetProperty("properties").TryGetProperty("hasMore", out _).Should().BeTrue();
        result.GetProperty("properties").TryGetProperty("totalCount", out _).Should().BeTrue();
        result.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).Should().NotContain("totalCount");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ObjectBoundedCollection_WHEN_ExportingValueSchema_THEN_ShouldPreserveItemProperties()
    {
        var result = _target.GetValueSchema<BoundedCollection<TestResponse>>();

        result.GetRawText().Should().Contain("value");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_NullableValueContract_WHEN_ExportingValueSchema_THEN_ShouldNormalizeObjectType()
    {
        var result = _target.GetValueSchema<TestStruct?>();

        result.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PreviouslyExportedContract_WHEN_ExportingAgain_THEN_ShouldReturnCachedSchema()
    {
        var first = _target.GetValueSchema<TestResponse>();

        var second = _target.GetValueSchema<TestResponse>();

        second.GetRawText().Should().Be(first.GetRawText());
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

    private static void AssertStringEnum(JsonElement schema, params string[] expectedValues)
    {
        schema.GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Equal(expectedValues);
    }

#pragma warning disable CA1812 // Schema fixtures are consumed through type metadata without construction.
    private sealed record TestRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed record EnumRequest
    {
        public required TestEnum Value { get; init; }
    }

    private sealed record EnumResponse
    {
        public required TestEnum Value { get; init; }
    }

    private sealed record AnnotatedRequest
    {
        [Range(1, 10)]
        public int Range { get; init; } = 1;

        [StringLength(10, MinimumLength = 2)]
        public string? Text { get; init; }

        [AllowedValues("First", "Second")]
        public string? Choice { get; init; }
    }

    private sealed record PresenceRequest
    {
        [Required]
        public string? DataAnnotatedRequired { get; init; }

        public required string RequiredNonNullable { get; init; }

        public required string? RequiredNullable { get; init; }

        [NotNull]
        public string? NotNullAnnotated { get; init; }
    }

    private sealed record DefaultedRequest
    {
        [DefaultValue(25)]
        public int? Limit { get; init; } = 25;
    }

#pragma warning restore CA1812

    private readonly record struct TestStruct
    {
        public string Value { get; init; }
    }

    private enum TestEnum
    {
        First,
        Second,
    }
}
