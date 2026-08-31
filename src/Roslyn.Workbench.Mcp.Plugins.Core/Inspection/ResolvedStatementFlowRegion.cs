namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Identifies one statement or a contiguous statement range accepted for Roslyn flow analysis.
/// </summary>
internal sealed class ResolvedStatementFlowRegion : ResolvedFlowRegion
{
    /// <summary>
    /// Gets the first statement.
    /// </summary>
    public StatementSyntax FirstStatement { get; }

    /// <summary>
    /// Gets the last statement.
    /// </summary>
    public StatementSyntax LastStatement { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolvedStatementFlowRegion"/> class.
    /// </summary>
    /// <param name="firstStatement">The first statement in the selected region.</param>
    /// <param name="lastStatement">The last statement in the selected region.</param>
    /// <param name="semanticModel">The semantic model used to analyse the statements.</param>
    /// <param name="resolvedLocation">The canonical source location of the statement range.</param>
    public ResolvedStatementFlowRegion(
        StatementSyntax firstStatement,
        StatementSyntax lastStatement,
        SemanticModel semanticModel,
        ResolvedLocation resolvedLocation)
        : base(semanticModel, resolvedLocation)
    {
        FirstStatement = firstStatement;
        LastStatement = lastStatement;
    }
}
