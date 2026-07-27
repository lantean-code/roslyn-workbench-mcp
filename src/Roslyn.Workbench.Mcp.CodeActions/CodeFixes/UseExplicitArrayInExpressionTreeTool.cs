namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class UseExplicitArrayInExpressionTreeTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.UseExplicitArrayInExpressionTree.CSharpUseExplicitArrayInExpressionTreeCodeFixProvider";
    private const string _diagnosticId = "CS9226";

    public UseExplicitArrayInExpressionTreeTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
