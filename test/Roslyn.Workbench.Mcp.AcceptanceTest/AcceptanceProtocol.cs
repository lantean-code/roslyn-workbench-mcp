using System.Text.Json;

using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed record AcceptanceWorkspaceIdentity
{
    private AcceptanceWorkspaceIdentity(string workspaceId, long workspaceEpoch)
    {
        WorkspaceId = workspaceId;
        WorkspaceEpoch = workspaceEpoch;
    }

    public string WorkspaceId { get; }

    public long WorkspaceEpoch { get; }

    public static AcceptanceWorkspaceIdentity FromOpenResult(CallToolResult result)
    {
        var workspace = AcceptanceProtocol.GetSuccessData(result).GetProperty("workspace");
        var workspaceId = workspace.GetProperty("workspaceId").GetString()
            ?? throw new InvalidOperationException("The workspace-open response did not contain a workspace ID.");

        return new AcceptanceWorkspaceIdentity(
            workspaceId,
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
