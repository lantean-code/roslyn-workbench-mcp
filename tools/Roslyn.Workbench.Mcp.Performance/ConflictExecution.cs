namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record ConflictExecution
{
    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required double CommitMilliseconds { get; init; }

    public required double ConflictDetectionMilliseconds { get; init; }

    public required double RecoveryMilliseconds { get; init; }

    public required string ErrorCode { get; init; }

    public required string? RequiredAction { get; init; }

    public required ExternalFileMutation ExternalMutation { get; init; }
}
