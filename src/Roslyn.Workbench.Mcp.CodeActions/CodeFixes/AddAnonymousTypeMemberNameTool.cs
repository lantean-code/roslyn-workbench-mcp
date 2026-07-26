namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AddAnonymousTypeMemberNameTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.AddAnonymousTypeMemberName.CSharpAddAnonymousTypeMemberNameCodeFixProvider";
    private const string _diagnosticId = "CS0746";

    public AddAnonymousTypeMemberNameTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticId)
    {
    }
}
