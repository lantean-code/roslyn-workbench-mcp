namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Identifies an expression accepted as a Roslyn data-flow analysis region.
/// </summary>
internal sealed class ResolvedExpressionFlowRegion : ResolvedFlowRegion
{
    /// <summary>
    /// Gets the expression.
    /// </summary>
    public ExpressionSyntax Expression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolvedExpressionFlowRegion"/> class.
    /// </summary>
    /// <param name="expression">The expression selected for analysis.</param>
    /// <param name="semanticModel">The semantic model used to analyse the expression.</param>
    /// <param name="resolvedLocation">The canonical source location of the expression.</param>
    public ResolvedExpressionFlowRegion(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        ResolvedLocation resolvedLocation)
        : base(semanticModel, resolvedLocation)
    {
        Expression = expression;
    }
}
