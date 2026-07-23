namespace Roslyn.Workbench.Mcp.ScenarioRunner;

#pragma warning disable CA1812 // The root instance is created by System.Text.Json deserialisation.
internal sealed record ScenarioSuite
{
    public required IReadOnlyList<RepositoryDefinition> Repositories { get; init; }
}
#pragma warning restore CA1812
