namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record CodeActionSelectionLocation
{
    public required string Path { get; init; }

    public required int Start { get; init; }

    public int? Length { get; init; }
}
#pragma warning restore CA1812
