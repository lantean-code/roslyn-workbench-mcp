namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class UseInterpolatedVerbatimStringTool : FixedCompilerCodeFixTool
{
    public UseInterpolatedVerbatimStringTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.UseInterpolatedVerbatimString.CSharpUseInterpolatedVerbatimStringCodeFixProvider",
            "CS8401")
    {
    }
}
