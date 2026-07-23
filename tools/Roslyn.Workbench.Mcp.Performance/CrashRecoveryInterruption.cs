namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record CrashRecoveryInterruption
{
    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required double InterruptionMilliseconds { get; init; }

    public required string AppliedTargetPath { get; init; }

    public required RecoveryEvidence RecoveryEvidence { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }
}
