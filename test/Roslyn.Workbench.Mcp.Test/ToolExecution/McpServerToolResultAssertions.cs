namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

internal static class McpServerToolResultAssertions
{
    public static void AssertTextContentMatchesStructuredContent(CallToolResult result)
    {
        result.StructuredContent.Should().NotBeNull();
        result.Content.Should().ContainSingle();
        var textContent = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        var structuredContent = result.StructuredContent.GetValueOrDefault();

        textContent.Text.Should().Be(structuredContent.GetRawText());
    }

    public static void AssertUnhandledFailure(CallToolResult result)
    {
        AssertTextContentMatchesStructuredContent(result);
        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("UnhandledException");
        result.StructuredContent.Value.GetProperty("error").GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
