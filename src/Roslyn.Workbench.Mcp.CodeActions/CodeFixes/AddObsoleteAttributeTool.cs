namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AddObsoleteAttributeTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider";

    private static readonly IReadOnlyList<string> _diagnosticIds =
    [
        "CS0612",
        "CS0618",
        "CS0672",
        "CS1062",
        "CS1064",
    ];

    public AddObsoleteAttributeTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticIds)
    {
    }
}
