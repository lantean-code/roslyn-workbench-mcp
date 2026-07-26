namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class UseExplicitTypeForConstTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.UseExplicitTypeForConst.UseExplicitTypeForConstCodeFixProvider";
    private const string _diagnosticId = "CS0822";

    public UseExplicitTypeForConstTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
