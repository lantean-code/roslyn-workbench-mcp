using System.Text.Json;

using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed record AcceptanceWorkspaceIdentity
{
    private AcceptanceWorkspaceIdentity(
        Guid workspaceId,
        string? alias,
        string loadedPath,
        long workspaceEpoch,
        Guid snapshotId)
    {
        WorkspaceId = workspaceId;
        Alias = alias;
        LoadedPath = loadedPath;
        WorkspaceEpoch = workspaceEpoch;
        SnapshotId = snapshotId;
    }

    public Guid WorkspaceId { get; }

    public string? Alias { get; }

    public string LoadedPath { get; }

    public long WorkspaceEpoch { get; }

    public Guid SnapshotId { get; }

    public static AcceptanceWorkspaceIdentity FromOpenResult(CallToolResult result)
    {
        var workspace = AcceptanceProtocol.GetSuccessData(result).GetProperty("workspace");
        var workspaceId = workspace.GetProperty("workspaceId").GetGuid();
        if (workspaceId == Guid.Empty)
        {
            throw new InvalidOperationException("The workspace-open response contained an empty workspace ID.");
        }

        var snapshot = AcceptanceProtocol.GetSnapshot(result);

        return new AcceptanceWorkspaceIdentity(
            workspaceId,
            workspace.TryGetProperty("alias", out var alias) ? alias.GetString() : null,
            workspace.GetProperty("loadedPath").GetString()
                ?? throw new InvalidOperationException("The workspace-open response did not contain a loaded path."),
            workspace.GetProperty("workspaceEpoch").GetInt64(),
            snapshot["snapshotId"] is Guid snapshotId
                ? snapshotId
                : throw new InvalidOperationException("The workspace-open response did not contain a snapshot ID."));
    }

    public Dictionary<string, object?> CreateSelector()
    {
        return new Dictionary<string, object?>
        {
            ["workspaceId"] = WorkspaceId,
        };
    }

    public Dictionary<string, object?> CreateSnapshot(int? transactionRevision)
    {
        return new Dictionary<string, object?>
        {
            ["workspaceId"] = WorkspaceId,
            ["workspaceEpoch"] = WorkspaceEpoch,
            ["snapshotId"] = SnapshotId,
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

    public static JsonElement GetContinuation(CallToolResult result)
    {
        return result.StructuredContent?.GetProperty("continuation")
            ?? throw new InvalidOperationException("The failed MCP result did not contain a structured continuation.");
    }

    public static Dictionary<string, object?> GetSnapshot(CallToolResult result)
    {
        var snapshot = result.StructuredContent?.GetProperty("snapshot")
            ?? throw new InvalidOperationException("The successful MCP result did not contain a snapshot.");

        return new Dictionary<string, object?>
        {
            ["workspaceId"] = snapshot.GetProperty("workspaceId").GetGuid(),
            ["workspaceEpoch"] = snapshot.GetProperty("workspaceEpoch").GetInt64(),
            ["snapshotId"] = snapshot.GetProperty("snapshotId").GetGuid(),
            ["transactionRevision"] = snapshot.GetProperty("transactionRevision").ValueKind == JsonValueKind.Null
                ? null
                : snapshot.GetProperty("transactionRevision").GetInt32(),
        };
    }
}
