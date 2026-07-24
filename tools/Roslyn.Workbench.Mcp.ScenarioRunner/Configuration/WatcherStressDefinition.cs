namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record WatcherStressDefinition
{
    public required string ArtifactPath { get; init; }

    public required int FileCount { get; init; }

    public required int WritePasses { get; init; }
}
#pragma warning restore CA1812
