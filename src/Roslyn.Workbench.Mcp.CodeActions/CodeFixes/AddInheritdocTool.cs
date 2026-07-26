namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AddInheritdocTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddInheritdoc.AddInheritdocCodeFixProvider";
    private const string _diagnosticId = "CS1591";

    public AddInheritdocTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
