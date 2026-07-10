using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

internal static class McpServerToolTestData
{
    public static Dictionary<string, JsonElement> CreateArguments(bool includeWorkspace = false)
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        };

        if (includeWorkspace)
        {
            arguments["workspace"] = JsonSerializer.SerializeToElement(new WorkspaceSelector
            {
                WorkspaceId = "WorkspaceId",
            });
        }

        return arguments;
    }

    public static Tool CreateProtocolTool(string name)
    {
        return new Tool
        {
            Name = name,
            Description = "Description",
        };
    }
}
