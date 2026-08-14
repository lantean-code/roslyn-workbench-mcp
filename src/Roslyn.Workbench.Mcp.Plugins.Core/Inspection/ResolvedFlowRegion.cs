namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal abstract class ResolvedFlowRegion
{
    public SemanticModel SemanticModel { get; }

    public ResolvedLocation ResolvedLocation { get; }

    protected ResolvedFlowRegion(SemanticModel semanticModel, ResolvedLocation resolvedLocation)
    {
        SemanticModel = semanticModel;
        ResolvedLocation = resolvedLocation;
    }
}
