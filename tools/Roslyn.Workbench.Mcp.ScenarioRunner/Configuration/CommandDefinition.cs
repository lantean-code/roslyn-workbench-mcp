namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record CommandDefinition
{
    public required string FileName { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public string? WindowsFileName { get; init; }

    public IReadOnlyList<string>? WindowsArguments { get; init; }
}
#pragma warning restore CA1812
