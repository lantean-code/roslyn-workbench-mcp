namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class DisambiguateSameVariableTool : FixedCompilerCodeFixTool
{
    public DisambiguateSameVariableTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.DisambiguateSameVariable.CSharpDisambiguateSameVariableCodeFixProvider",
            ["CS1717", "CS1718"])
    {
    }
}
