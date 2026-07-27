namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class MakeRefStructTool : FixedCompilerCodeFixTool
{
    public MakeRefStructTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.MakeRefStruct.MakeRefStructCodeFixProvider",
            "CS8345")
    {
    }
}
