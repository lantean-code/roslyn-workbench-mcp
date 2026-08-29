using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedErrorRecord
{
    /// <summary>
    /// Gets the Correlation Id.
    /// </summary>
    [Description("Correlation identifier assigned to the captured failure.")]
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Gets the Failure Time.
    /// </summary>
    [Description("Time at which the tool invocation failed.")]
    public required DateTimeOffset FailureTime { get; init; }

    /// <summary>
    /// Gets the Expires At.
    /// </summary>
    [Description("Time at which this locally retained error record expires.")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the Tool Name.
    /// </summary>
    [Description("MCP tool that failed.")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the Execution Family.
    /// </summary>
    [Description("Execution path used by the failed tool, such as server, plugin, or Code Action.")]
    public required string ExecutionFamily { get; init; }

    /// <summary>
    /// Gets the Plugin Classification.
    /// </summary>
    [Description("Plugin ownership classification when the failed tool was plugin-backed.")]
    public required string PluginClassification { get; init; }

    /// <summary>
    /// Gets the Duration Milliseconds.
    /// </summary>
    [Description("Tool execution duration in milliseconds before failure.")]
    public required long DurationMilliseconds { get; init; }

    /// <summary>
    /// Gets the Cancellation Requested.
    /// </summary>
    [Description("Whether cancellation had been requested when the failure occurred.")]
    public bool CancellationRequested { get; init; }

    /// <summary>
    /// Gets the Exceptions.
    /// </summary>
    [Description("Captured exception chain for the failure.")]
    public ImmutableArray<CapturedException> Exceptions { get; init; } = [];

    /// <summary>
    /// Gets the Workspace.
    /// </summary>
    [Description("Workspace context active during the failure, when available.")]
    public CapturedWorkspaceContext? Workspace { get; init; }

    /// <summary>
    /// Gets the Server Version.
    /// </summary>
    [Description("Roslyn Workbench server version that captured the failure.")]
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Gets the Roslyn Version.
    /// </summary>
    [Description("Roslyn version used by the server.")]
    public required string RoslynVersion { get; init; }

    /// <summary>
    /// Gets the Dot Net Version.
    /// </summary>
    [Description(".NET runtime version used by the server.")]
    public required string DotNetVersion { get; init; }

    /// <summary>
    /// Gets the Operating System.
    /// </summary>
    [Description("Operating system on which the server was running.")]
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// Gets the Processor Architecture.
    /// </summary>
    [Description("Processor architecture of the server process.")]
    public required string ProcessorArchitecture { get; init; }
}
