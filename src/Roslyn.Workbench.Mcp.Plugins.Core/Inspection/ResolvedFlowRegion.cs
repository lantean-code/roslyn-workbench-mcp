namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Provides the semantic model and canonical source location shared by resolved flow-analysis regions.
/// </summary>
internal abstract class ResolvedFlowRegion
{
    /// <summary>
    /// Gets the semantic model.
    /// </summary>
    public SemanticModel SemanticModel { get; }

    /// <summary>
    /// Gets the resolved location.
    /// </summary>
    public ResolvedLocation ResolvedLocation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolvedFlowRegion"/> class.
    /// </summary>
    /// <param name="semanticModel">The semantic model used to analyse the region.</param>
    /// <param name="resolvedLocation">The canonical source location of the region.</param>
    protected ResolvedFlowRegion(SemanticModel semanticModel, ResolvedLocation resolvedLocation)
    {
        SemanticModel = semanticModel;
        ResolvedLocation = resolvedLocation;
    }
}
