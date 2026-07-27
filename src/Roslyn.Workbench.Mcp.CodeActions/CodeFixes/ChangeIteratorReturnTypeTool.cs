namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class ChangeIteratorReturnTypeTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider";
    private const string _diagnosticId = "CS1624";

    public ChangeIteratorReturnTypeTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
