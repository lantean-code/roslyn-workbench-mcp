namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class FixReturnTypeTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixReturnType.CSharpFixReturnTypeCodeFixProvider";

    private static readonly IReadOnlyList<string> _diagnosticIds =
    [
        "CS0127",
        "CS1997",
        "CS0201",
    ];

    public FixReturnTypeTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticIds)
    {
    }
}
