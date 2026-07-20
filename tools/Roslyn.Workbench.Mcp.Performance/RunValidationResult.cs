namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record RunValidationResult
{
    public required DateTimeOffset CompletedAtUtc { get; init; }

    public required string ExpectedCommit { get; init; }

    public string? ActualCommit { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }

    public required IReadOnlyList<string> StateFiles { get; init; }

    public required IReadOnlyList<string> NewWorkspaceStateFiles { get; init; }

    public required IReadOnlyList<string> Issues { get; init; }

    public bool Succeeded => Issues.Count == 0;
}
