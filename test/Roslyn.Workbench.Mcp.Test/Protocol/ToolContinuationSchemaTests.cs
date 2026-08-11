using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

[Trait("Category", "Contract")]
public sealed class ToolContinuationSchemaTests
{
    [Fact]
    public void GIVEN_PublishedContinuationContract_WHEN_CreatingSchema_THEN_ShouldDescribeEveryClosedVariant()
    {
        var result = ToolContinuationSchema.Create();

        var variants = result.GetProperty("oneOf").EnumerateArray().ToArray();
        variants.Should().HaveCount(5);

        AssertVariant(variants, "CallTool", "tool");
        AssertVariant(variants, "ChooseTool", "tools");
        AssertVariant(variants, "RetryRequest");
        AssertVariant(variants, "ReviseRequest");
        AssertVariant(variants, "ResolveExternally");

        var chooseTool = FindVariant(variants, "ChooseTool");
        var tools = chooseTool.GetProperty("properties").GetProperty("tools");
        tools.GetProperty("type").GetString().Should().Be("array");
        tools.GetProperty("minItems").GetInt32().Should().Be(1);
        tools.GetProperty("uniqueItems").GetBoolean().Should().BeTrue();
        tools.GetProperty("items").GetProperty("minLength").GetInt32().Should().Be(1);
    }

    private static void AssertVariant(IReadOnlyList<JsonElement> variants, string kind, string? actionProperty = null)
    {
        var variant = FindVariant(variants, kind);
        variant.GetProperty("type").GetString().Should().Be("object");
        variant.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();

        var required = variant.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).ToArray();
        required.Should().Contain(["kind", "instruction"]);

        var properties = variant.GetProperty("properties");
        properties.GetProperty("instruction").GetProperty("minLength").GetInt32().Should().Be(1);
        if (actionProperty is null)
        {
            properties.EnumerateObject().Select(static item => item.Name).Should().Equal("kind", "instruction");
            return;
        }

        required.Should().Contain(actionProperty);
        properties.TryGetProperty(actionProperty, out _).Should().BeTrue();
    }

    private static JsonElement FindVariant(IReadOnlyList<JsonElement> variants, string kind)
    {
        return variants.Single(
            item => item.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString() == kind);
    }
}
