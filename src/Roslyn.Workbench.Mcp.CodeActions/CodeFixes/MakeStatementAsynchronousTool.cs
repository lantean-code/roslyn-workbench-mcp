namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class MakeStatementAsynchronousTool : FixedCompilerCodeFixTool
{
    public MakeStatementAsynchronousTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeStatementAsynchronous.CSharpMakeStatementAsynchronousCodeFixProvider",
            ["CS8414", "CS8418"])
    {
    }
}
