using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal static class CallToolResultFactory
{
    public static CallToolResult CreateStructured(JsonElement structuredContent, bool isError)
    {
        var textContent = new TextContentBlock
        {
            Text = structuredContent.GetRawText(),
        };

        return new CallToolResult
        {
            Content = [textContent],
            StructuredContent = structuredContent,
            IsError = isError,
        };
    }
}
