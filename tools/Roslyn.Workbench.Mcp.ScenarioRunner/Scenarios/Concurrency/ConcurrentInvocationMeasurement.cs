namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;

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
