namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed record StateSequenceStepMeasurement
{
    public required string Name { get; init; }

    public required string Tool { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required bool IsError { get; init; }

    public required string ResponseSha256 { get; init; }

    public string? ErrorCode { get; init; }

    public string? RequiredAction { get; init; }

    public bool? MutationStaged { get; init; }

    public int? ReferenceCount { get; init; }

    public IReadOnlyList<string> DefinitionPaths { get; init; } = [];

    public int? TransactionRevision { get; init; }

    public int? TransactionRevisionCount { get; init; }

    public bool? CanUndo { get; init; }

    public bool? CanRedo { get; init; }
}
