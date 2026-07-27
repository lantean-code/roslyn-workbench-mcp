namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class UnsealClassTool : FixedCompilerCodeFixTool
{
    public UnsealClassTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.UnsealClass.CSharpUnsealClassCodeFixProvider",
            "CS0509")
    {
    }
}
