namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record ExternalWildcardStressDefinition
{
    public required int FilesPerGlob { get; init; }

    public required int GlobsPerRoot { get; init; }

    public required int RootCount { get; init; }

    public required string TargetProjectPath { get; init; }
}
#pragma warning restore CA1812
