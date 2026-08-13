using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Conflict;

internal sealed record ConflictMeasurement
{
    public required int Iteration { get; init; }

    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required double CommitMilliseconds { get; init; }

    public required double ConflictDetectionMilliseconds { get; init; }

    public required double RecoveryMilliseconds { get; init; }

    public required double RestorationMilliseconds { get; init; }

    public required string ErrorCode { get; init; }

    public required ToolContinuationObservation? Continuation { get; init; }

    public required ExternalFileMutation ExternalMutation { get; init; }

    public required IReadOnlyList<DurableCommitFileChange> FilesBeforeRestoration { get; init; }

    public required string? RecoveryState { get; init; }

    public required int RecoveryArtifactCount { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }

    public required RunValidationResult Validation { get; init; }
}
