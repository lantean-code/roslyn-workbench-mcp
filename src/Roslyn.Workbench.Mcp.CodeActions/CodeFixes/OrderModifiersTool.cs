namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class OrderModifiersTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.OrderModifiers.CSharpOrderModifiersCodeFixProvider";
    private const string _diagnosticId = "CS0267";

    public OrderModifiersTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
