namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class ResolvedStatementFlowRegion : ResolvedFlowRegion
{
    public StatementSyntax FirstStatement { get; }

    public StatementSyntax LastStatement { get; }

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
