namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class MakeTypeAbstractTool : FixedCompilerCodeFixTool
{
    public MakeTypeAbstractTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.MakeTypeAbstract.CSharpMakeTypeAbstractCodeFixProvider",
            "CS0513")
    {
    }
}
