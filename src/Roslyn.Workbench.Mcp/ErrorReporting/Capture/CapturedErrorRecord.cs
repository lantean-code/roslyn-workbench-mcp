using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Retains the diagnostic details of a failed tool invocation until the user reviews or submits them.
/// </summary>
internal sealed record CapturedErrorRecord
{
    /// <summary>
    /// Correlation identifier assigned to the captured failure.
    /// </summary>
    [Description("Correlation identifier assigned to the captured failure.")]
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Time at which the tool invocation failed.
    /// </summary>
    [Description("Time at which the tool invocation failed.")]
    public required DateTimeOffset FailureTime { get; init; }

    /// <summary>
    /// Time at which this locally retained error record expires.
    /// </summary>
    [Description("Time at which this locally retained error record expires.")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// MCP tool that failed.
    /// </summary>
    [Description("MCP tool that failed.")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Execution path used by the failed tool, such as server, plugin, or Code Action.
    /// </summary>
    [Description("Execution path used by the failed tool, such as server, plugin, or Code Action.")]
    public required string ExecutionFamily { get; init; }

    /// <summary>
    /// Plugin ownership classification when the failed tool was plugin-backed.
    /// </summary>
    [Description("Plugin ownership classification when the failed tool was plugin-backed.")]
    public required string PluginClassification { get; init; }

    /// <summary>
    /// Tool execution duration in milliseconds before failure.
    /// </summary>
    [Description("Tool execution duration in milliseconds before failure.")]
    public required long DurationMilliseconds { get; init; }

    /// <summary>
    /// Whether cancellation had been requested when the failure occurred.
    /// </summary>
    [Description("Whether cancellation had been requested when the failure occurred.")]
    public bool CancellationRequested { get; init; }

    /// <summary>
    /// Captured exception chain for the failure.
    /// </summary>
    [Description("Captured exception chain for the failure.")]
    public ImmutableArray<CapturedException> Exceptions { get; init; } = [];

    /// <summary>
    /// Workspace context active during the failure, when available.
    /// </summary>
    [Description("Workspace context active during the failure, when available.")]
    public CapturedWorkspaceContext? Workspace { get; init; }

    /// <summary>
    /// Roslyn Workbench server version that captured the failure.
    /// </summary>
    [Description("Roslyn Workbench server version that captured the failure.")]
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Roslyn version used by the server.
    /// </summary>
    [Description("Roslyn version used by the server.")]
    public required string RoslynVersion { get; init; }

    /// <summary>
    /// .NET runtime version used by the server.
    /// </summary>
    [Description(".NET runtime version used by the server.")]
    public required string DotNetVersion { get; init; }

    /// <summary>
    /// Operating system on which the server was running.
    /// </summary>
    [Description("Operating system on which the server was running.")]
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// Processor architecture of the server process.
    /// </summary>
    [Description("Processor architecture of the server process.")]
    public required string ProcessorArchitecture { get; init; }
}
