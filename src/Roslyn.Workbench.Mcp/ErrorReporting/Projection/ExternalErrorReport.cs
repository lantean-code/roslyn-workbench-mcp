using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal sealed record ExternalErrorReport
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentReportFormatVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public int ReportFormatVersion { get; init; } = CurrentReportFormatVersion;

    public required string ReportId { get; init; }

    public required DateTimeOffset FailureTime { get; init; }

    public required string Tool { get; init; }

    public required string ExecutionFamily { get; init; }

    public required string PluginClassification { get; init; }

    public required long DurationMilliseconds { get; init; }

    public bool CancellationRequested { get; init; }

    public required string ExceptionClassification { get; init; }

    public ImmutableArray<ExternalStackFrame> StackFrames { get; init; } = [];

    public ExternalWorkspaceContext? Workspace { get; init; }

    public required string ServerVersion { get; init; }

    public required string RoslynVersion { get; init; }

    public required string DotNetVersion { get; init; }

    public required string OperatingSystem { get; init; }

    public required string ProcessorArchitecture { get; init; }
}
