namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class RemoveInKeywordTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.RemoveInKeyword.RemoveInKeywordCodeFixProvider";
    private const string _diagnosticId = "CS1615";

    public RemoveInKeywordTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
