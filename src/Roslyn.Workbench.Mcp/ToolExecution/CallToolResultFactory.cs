using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution;

/// <summary>
/// Creates MCP call results whose text and structured representations contain the same JSON payload.
/// </summary>
internal static class CallToolResultFactory
{
    /// <summary>
    /// Creates an MCP call result with matching textual and structured JSON content.
    /// </summary>
    /// <param name="structuredContent">The structured MCP content used to construct the tool result.</param>
    /// <param name="isError">Whether the protocol result represents an error.</param>
    /// <returns>A call result containing the supplied structured content and error classification.</returns>
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
