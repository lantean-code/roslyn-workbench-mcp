namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AddDocumentationCommentNodesTool : FixedCompilerCodeFixTool
{
    public AddDocumentationCommentNodesTool(ILocationCodeFixStager locationFixStager)
        : base(
            locationFixStager,
            "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpAddDocCommentNodesCodeFixProvider",
            "CS1573")
    {
    }
}
