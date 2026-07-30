using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.IntegrationTest.Protocol;

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
    [Trait("Category", "Integration")]
    public void GIVEN_PresenceAndNullabilityContracts_WHEN_ExportingInputSchema_THEN_ShouldPublishPresenceButLoseNullabilityState()
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
        AllowsNull(properties.GetProperty("requiredNonNullable")).Should().BeTrue();
        AllowsNull(properties.GetProperty("requiredNullable")).Should().BeTrue();
        AllowsNull(properties.GetProperty("notNullAnnotated")).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PropertyDefaultValueAttribute_WHEN_ExportingInputSchema_THEN_ShouldNotPublishPropertyDefault()
    {
        var result = _target.GetInputSchema<DefaultedRequest>();

        result.GetProperty("properties")
            .GetProperty("limit")
            .TryGetProperty("default", out _)
            .Should()
            .BeFalse();
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

#pragma warning disable CA1812 // Schema fixtures are consumed through type metadata without construction.
    private sealed record TestRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
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
}
