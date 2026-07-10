using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Tools;

internal static class ServerOwnedToolTestData
{
    public static WorkspaceSelector CreateWorkspaceSelector()
    {
        return new WorkspaceSelector
        {
            WorkspaceId = "WorkspaceId",
            Alias = "Alias",
            Path = "/workspace/Sample.csproj",
        };
    }

    public static Dictionary<string, JsonElement> CreateWorkspaceArguments(bool includeWorkspace)
    {
        var arguments = new Dictionary<string, JsonElement>();
        if (includeWorkspace)
        {
            arguments["workspace"] = JsonSerializer.SerializeToElement(CreateWorkspaceSelector());
        }

        return arguments;
    }

    public static string? GetWorkspaceId(bool includeWorkspace)
    {
        return includeWorkspace ? "WorkspaceId" : null;
    }

    public static string? GetWorkspaceAlias(bool includeWorkspace)
    {
        return includeWorkspace ? "Alias" : null;
    }

    public static string? GetWorkspacePath(bool includeWorkspace)
    {
        return includeWorkspace ? "/workspace/Sample.csproj" : null;
    }
}
