namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed record ConcurrencyRunResult
{
    public required string Repository { get; init; }

    public required string RepositorySize { get; init; }

    public required string Commit { get; init; }

    public required string Scenario { get; init; }

    public required string Tool { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required RunEnvironmentInfo Environment { get; init; }

    public required int WarmupCount { get; init; }

    public required int Parallelism { get; init; }

    public required IReadOnlyList<ConcurrentBatchMeasurement> Batches { get; init; }

    public required MultiWorkspaceMeasurement MultiWorkspace { get; init; }
}

internal sealed record ConcurrentBatchMeasurement
{
    public required int Iteration { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required IReadOnlyList<ConcurrentInvocationMeasurement> Invocations { get; init; }
}

internal sealed record ConcurrentInvocationMeasurement
{
    public required int Slot { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required int ResponseBytes { get; init; }

    public required string ResponseSha256 { get; init; }

    public required bool IsError { get; init; }

    public string? ErrorCode { get; init; }

    public string? RequiredAction { get; init; }

    public required bool RetrySucceeded { get; init; }
}

internal sealed record MultiWorkspaceMeasurement
{
    public required string PrimaryWorkspaceId { get; init; }

    public required string SecondaryWorkspaceId { get; init; }

    public required int ListedWorkspaceCount { get; init; }

    public required double ParallelQueryElapsedMilliseconds { get; init; }

    public required IReadOnlyList<ConcurrencyStepMeasurement> Steps { get; init; }
}

internal sealed record ConcurrencyStepMeasurement
{
    public required string Name { get; init; }

    public required string Tool { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required bool IsError { get; init; }

    public required int ResponseBytes { get; init; }

    public required string ResponseSha256 { get; init; }

    public string? ErrorCode { get; init; }

    public string? RequiredAction { get; init; }

    public int? WorkspaceCount { get; init; }
}

internal sealed record ConcurrencyExecution
{
    public required IReadOnlyList<ConcurrentBatchMeasurement> Batches { get; init; }

    public required MultiWorkspaceMeasurement MultiWorkspace { get; init; }
}
