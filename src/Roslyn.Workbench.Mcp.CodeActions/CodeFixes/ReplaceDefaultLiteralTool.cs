namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class ReplaceDefaultLiteralTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.ReplaceDefaultLiteral.CSharpReplaceDefaultLiteralCodeFixProvider";
    private const string _diagnosticId = "CS8505";

    public ReplaceDefaultLiteralTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
