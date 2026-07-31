namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;

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

    public string? Workload { get; init; }

    public int? FactoryExecutionCount { get; init; }

    public int? PayloadLength { get; init; }
}
