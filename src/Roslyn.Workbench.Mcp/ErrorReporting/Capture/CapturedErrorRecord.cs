using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedErrorRecord
{
    public required Guid CorrelationId { get; init; }

    public required DateTimeOffset FailureTime { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string ToolName { get; init; }

    public required string ExecutionFamily { get; init; }

    public required string PluginClassification { get; init; }

    public required long DurationMilliseconds { get; init; }

    public bool CancellationRequested { get; init; }

    public ImmutableArray<CapturedException> Exceptions { get; init; } = [];

    public CapturedWorkspaceContext? Workspace { get; init; }

    public required string ServerVersion { get; init; }

    public required string RoslynVersion { get; init; }

    public required string DotNetVersion { get; init; }

    public required string OperatingSystem { get; init; }

    public required string ProcessorArchitecture { get; init; }
}
