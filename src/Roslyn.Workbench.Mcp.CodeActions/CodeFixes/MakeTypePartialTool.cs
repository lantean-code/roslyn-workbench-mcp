namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class MakeTypePartialTool : FixedCompilerCodeFixTool
{
    public MakeTypePartialTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.MakeTypePartial.CSharpMakeTypePartialCodeFixProvider",
            "CS0260")
    {
    }
}
