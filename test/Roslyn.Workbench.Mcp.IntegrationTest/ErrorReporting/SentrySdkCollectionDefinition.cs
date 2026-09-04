namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SentrySdkCollectionDefinition
{
    public const string Name = "Sentry SDK";
}
