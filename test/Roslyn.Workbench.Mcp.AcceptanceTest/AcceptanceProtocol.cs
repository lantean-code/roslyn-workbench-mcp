using System.Text.Json;

using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed record AcceptanceWorkspaceIdentity
{
    private AcceptanceWorkspaceIdentity(
        Guid workspaceId,
        string? alias,
        string loadedPath,
        long workspaceEpoch)
    {
        WorkspaceId = workspaceId;
        Alias = alias;
        LoadedPath = loadedPath;
        WorkspaceEpoch = workspaceEpoch;
    }

    public Guid WorkspaceId { get; }

    public string? Alias { get; }

    public string LoadedPath { get; }

    public long WorkspaceEpoch { get; }

    public static AcceptanceWorkspaceIdentity FromOpenResult(CallToolResult result)
    {
        var workspace = AcceptanceProtocol.GetSuccessData(result).GetProperty("workspace");
        var workspaceId = workspace.GetProperty("workspaceId").GetGuid();
        if (workspaceId == Guid.Empty)
        {
            throw new InvalidOperationException("The workspace-open response contained an empty workspace ID.");
        }

        return new AcceptanceWorkspaceIdentity(
            workspaceId,
            workspace.TryGetProperty("alias", out var alias) ? alias.GetString() : null,
            workspace.GetProperty("loadedPath").GetString()
                ?? throw new InvalidOperationException("The workspace-open response did not contain a loaded path."),
            workspace.GetProperty("workspaceEpoch").GetInt64());
    }

    public Dictionary<string, object?> CreateSelector()
    {
        return new Dictionary<string, object?>
        {
            ["workspaceId"] = WorkspaceId,
        };
    }

    public Dictionary<string, object?> CreateSnapshot(int transactionRevision)
    {
        return new Dictionary<string, object?>
        {
            ["workspaceId"] = WorkspaceId,
            ["workspaceEpoch"] = WorkspaceEpoch,
            ["transactionRevision"] = transactionRevision,
        };
    }
}

internal static class AcceptanceProtocol
{
    public static JsonElement GetSuccessData(CallToolResult result)
    {
        return result.StructuredContent?.GetProperty("data")
            ?? throw new InvalidOperationException("The successful MCP result did not contain structured data.");
    }

    public static JsonElement GetError(CallToolResult result)
    {
        return result.StructuredContent?.GetProperty("error")
            ?? throw new InvalidOperationException("The failed MCP result did not contain a structured error.");
    }
}
