using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

/// <summary>
/// Represents the privacy-filtered diagnostic payload eligible for external submission.
/// </summary>
internal sealed record ExternalErrorReport
{
    /// <summary>
    /// Defines the current version of the report's JSON structure.
    /// </summary>
    public const int CurrentSchemaVersion = 2;
    /// <summary>
    /// Defines the current semantic version of the diagnostic report format.
    /// </summary>
    public const int CurrentReportFormatVersion = 2;

    /// <summary>
    /// Gets the version of the report's JSON structure.
    /// </summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets the semantic version of the diagnostic report format.
    /// </summary>
    public int ReportFormatVersion { get; init; } = CurrentReportFormatVersion;

    /// <summary>
    /// Gets the opaque identifier assigned to this projected report.
    /// </summary>
    public required string ReportId { get; init; }

    /// <summary>
    /// Gets the time at which the tool invocation failed.
    /// </summary>
    public required DateTimeOffset FailureTime { get; init; }

    /// <summary>
    /// Gets the published tool name, or the generic external-plugin label when its identity is withheld.
    /// </summary>
    public required string Tool { get; init; }

    /// <summary>
    /// Gets the execution path used by the failed tool, such as server, plugin or Code Action.
    /// </summary>
    public required string ExecutionFamily { get; init; }

    /// <summary>
    /// Gets whether a plugin-backed tool is bundled, external or not applicable.
    /// </summary>
    public required string PluginClassification { get; init; }

    /// <summary>
    /// Gets the tool execution duration in milliseconds before failure.
    /// </summary>
    public required long DurationMilliseconds { get; init; }

    /// <summary>
    /// Gets a value indicating whether cancellation was requested for the operation.
    /// </summary>
    public bool CancellationRequested { get; init; }

    /// <summary>
    /// Gets the high-level component classification of the outermost exception.
    /// </summary>
    public required string ExceptionClassification { get; init; }

    /// <summary>
    /// Gets the filtered exception chain and diagnostic stack frames.
    /// </summary>
    public ImmutableArray<ExternalException> Exceptions { get; init; } = [];

    /// <summary>
    /// Gets non-identifying workspace state captured during the failure, when available.
    /// </summary>
    public ExternalWorkspaceContext? Workspace { get; init; }

    /// <summary>
    /// Gets the Roslyn Workbench server version that captured the failure.
    /// </summary>
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Gets the Roslyn version used by the server.
    /// </summary>
    public required string RoslynVersion { get; init; }

    /// <summary>
    /// Gets the .NET runtime version used by the server.
    /// </summary>
    public required string DotNetVersion { get; init; }

    /// <summary>
    /// Gets the operating-system family on which the server was running.
    /// </summary>
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// Gets the architecture of the server process.
    /// </summary>
    public required string ProcessorArchitecture { get; init; }
}
