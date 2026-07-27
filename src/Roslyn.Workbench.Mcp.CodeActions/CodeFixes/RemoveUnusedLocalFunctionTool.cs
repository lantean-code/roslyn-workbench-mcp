namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class RemoveUnusedLocalFunctionTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.RemoveUnusedLocalFunction.CSharpRemoveUnusedLocalFunctionCodeFixProvider";
    private const string _diagnosticId = "CS8321";

    public RemoveUnusedLocalFunctionTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
