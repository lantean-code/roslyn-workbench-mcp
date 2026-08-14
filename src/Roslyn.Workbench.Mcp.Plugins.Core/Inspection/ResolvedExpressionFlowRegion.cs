namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class ResolvedExpressionFlowRegion : ResolvedFlowRegion
{
    public ExpressionSyntax Expression { get; }

    public ResolvedExpressionFlowRegion(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        ResolvedLocation resolvedLocation)
        : base(semanticModel, resolvedLocation)
    {
        Expression = expression;
    }
}
