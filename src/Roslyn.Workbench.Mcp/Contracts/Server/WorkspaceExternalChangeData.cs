using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Describes the first external input change detected for a loaded workspace.
/// </summary>
internal sealed record WorkspaceExternalChangeData
{
    /// <summary>
    /// The mechanism that detected the change.
    /// </summary>
    [Description("The mechanism that detected the change.")]
    public required string DetectionSource { get; init; }

    /// <summary>
    /// The stable error classification when change detection itself failed.
    /// </summary>
    [Description("The stable error classification when change detection itself failed.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// The detected change kind.
    /// </summary>
    [Description("The detected change kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// The affected path, when the detection mechanism identified one.
    /// </summary>
    [Description("The affected path, when the detection mechanism identified one.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    /// <summary>
    /// The previous path for a rename, when present.
    /// </summary>
    [Description("The previous path for a rename, when present.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousPath { get; init; }
}
