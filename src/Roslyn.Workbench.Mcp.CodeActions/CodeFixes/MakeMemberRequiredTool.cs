namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class MakeMemberRequiredTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeMemberRequired.CSharpMakeMemberRequiredCodeFixProvider";
    private const string _diagnosticId = "CS8618";

    public MakeMemberRequiredTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
