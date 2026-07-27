namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class MakeMemberStaticTool : FixedCompilerCodeFixTool
{
    public MakeMemberStaticTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.MakeMemberStatic.CSharpMakeMemberStaticCodeFixProvider",
            "CS0708")
    {
    }
}
