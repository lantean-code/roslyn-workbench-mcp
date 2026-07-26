namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class RemoveNewModifierTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.RemoveNewModifier.RemoveNewModifierCodeFixProvider";
    private const string _diagnosticId = "CS0109";

    public RemoveNewModifierTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
