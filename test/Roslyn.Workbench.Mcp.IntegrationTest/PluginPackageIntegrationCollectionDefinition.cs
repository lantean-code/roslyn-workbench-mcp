namespace Roslyn.Workbench.Mcp.Test;

#pragma warning disable CA1515 // xUnit discovers collection definitions as public test-assembly metadata.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PluginPackageIntegrationCollectionDefinition
{
    public const string Name = "Plugin package integration";
}
#pragma warning restore CA1515
