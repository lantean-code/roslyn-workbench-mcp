namespace Roslyn.Workbench.Mcp.Test.ToolReference;

[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit requires collection definition types to be public.
public sealed class ToolReferenceGenerationCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "Tool reference generation";
}
