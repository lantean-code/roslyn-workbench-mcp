namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

internal static class McpServerToolResultAssertions
{
    public static void AssertUnhandledFailure(CallToolResult result)
    {
        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("UnhandledException");
        result.StructuredContent.Value.GetProperty("error").GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
