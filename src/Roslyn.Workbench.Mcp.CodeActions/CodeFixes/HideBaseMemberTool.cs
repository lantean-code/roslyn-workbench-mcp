namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class HideBaseMemberTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.HideBase.HideBaseCodeFixProvider";
    private const string _diagnosticId = "CS0108";

    public HideBaseMemberTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
