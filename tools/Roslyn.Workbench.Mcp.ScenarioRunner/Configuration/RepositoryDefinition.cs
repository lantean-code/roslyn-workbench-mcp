namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record RepositoryDefinition
{
    public required string Id { get; init; }

    public required string Size { get; init; }

    public required string Url { get; init; }

    public required string Commit { get; init; }

    public required string WorkspacePath { get; init; }

    public required IReadOnlyList<CommandDefinition> Preparation { get; init; }

    public required IReadOnlyList<ScenarioDefinition> Scenarios { get; init; }
}
#pragma warning restore CA1812
