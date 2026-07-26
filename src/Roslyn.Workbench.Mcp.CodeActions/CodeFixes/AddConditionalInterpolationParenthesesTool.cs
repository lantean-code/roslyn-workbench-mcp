namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AddConditionalInterpolationParenthesesTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.ConditionalExpressionInStringInterpolation.CSharpAddParenthesesAroundConditionalExpressionInInterpolatedStringCodeFixProvider";
    private const string _diagnosticId = "CS8361";

    public AddConditionalInterpolationParenthesesTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
