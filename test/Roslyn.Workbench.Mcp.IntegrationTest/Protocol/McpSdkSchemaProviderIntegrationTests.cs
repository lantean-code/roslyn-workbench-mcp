using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Collections;

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
    public void GIVEN_ObjectContract_WHEN_ExportingValueSchema_THEN_ShouldPublishProperties()
    {
        var result = _target.GetValueSchema<TestResponse>();

        result.GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PrimitiveBoundedCollection_WHEN_ExportingValueSchema_THEN_ShouldPublishItemsAndHasMore()
    {
        var result = _target.GetValueSchema<BoundedCollection<string>>();

        result.GetProperty("properties").TryGetProperty("items", out _).Should().BeTrue();
        result.GetProperty("properties").TryGetProperty("hasMore", out _).Should().BeTrue();
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

#pragma warning disable CA1812 // Schema fixtures are consumed through type metadata without construction.
    private sealed record TestRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
#pragma warning restore CA1812

    private readonly record struct TestStruct
    {
        public string Value { get; init; }
    }
}
