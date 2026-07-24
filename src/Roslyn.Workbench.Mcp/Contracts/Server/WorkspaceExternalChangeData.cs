using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Describes the first external Workspace input change detected for a loaded Workspace.
/// </summary>
internal sealed record WorkspaceExternalChangeData
{
    /// <summary>
    /// Gets the mechanism that detected the change.
    /// </summary>
    public required string DetectionSource { get; init; }

    /// <summary>
    /// Gets the stable error classification when change detection itself failed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the detected change kind.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the affected path, when the detection mechanism identified one.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    /// <summary>
    /// Gets the previous path for a rename, when present.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousPath { get; init; }
}
