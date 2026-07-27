namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class PassCapturedVariablesAsArgumentsTool : FixedCompilerCodeFixTool
{
    public PassCapturedVariablesAsArgumentsTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.PassInCapturedVariablesAsArgumentsCodeFixProvider",
            "CS8421")
    {
    }
}
