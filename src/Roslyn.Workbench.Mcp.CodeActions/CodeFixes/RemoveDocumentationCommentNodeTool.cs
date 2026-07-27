namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class RemoveDocumentationCommentNodeTool : FixedCompilerCodeFixTool
{
    public RemoveDocumentationCommentNodeTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpRemoveDocCommentNodeCodeFixProvider",
            ["CS1571", "CS1572", "CS1710"])
    {
    }
}
