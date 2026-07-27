namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class TransposeRecordKeywordTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.TransposeRecordKeyword.CSharpTransposeRecordKeywordCodeFixProvider";
    private const string _diagnosticId = "CS9012";

    public TransposeRecordKeywordTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
