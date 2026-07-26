namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AddExplicitCastTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddExplicitCast.CSharpAddExplicitCastCodeFixProvider";
    private const string _diagnosticId = "CS0266";

    public AddExplicitCastTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
