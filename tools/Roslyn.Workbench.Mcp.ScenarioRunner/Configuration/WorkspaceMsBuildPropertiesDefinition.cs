namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record WorkspaceMsBuildPropertiesDefinition
{
    public string? TargetFramework { get; init; }
}
#pragma warning restore CA1812
