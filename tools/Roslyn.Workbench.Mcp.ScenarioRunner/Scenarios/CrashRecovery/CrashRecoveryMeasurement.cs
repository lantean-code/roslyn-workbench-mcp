using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CrashRecovery;

internal sealed record CrashRecoveryMeasurement
{
    public required int Iteration { get; init; }

    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required double InterruptionMilliseconds { get; init; }

    public required double RecoveryStartupMilliseconds { get; init; }

    public required double WorkspaceReopenMilliseconds { get; init; }

    public required double RunnerCleanupMilliseconds { get; init; }

    public required string AppliedTargetPath { get; init; }

    public required IReadOnlyList<DurableCommitFileChange> FilesBeforeRecovery { get; init; }

    public required string? PreparedRecoveryState { get; init; }

    public required int PreparedRecoveryArtifactCount { get; init; }

    public required HostShutdownResult InterruptedHostShutdown { get; init; }

    public required HostShutdownResult RecoveryHostShutdown { get; init; }

    public required RunValidationResult Validation { get; init; }
}
