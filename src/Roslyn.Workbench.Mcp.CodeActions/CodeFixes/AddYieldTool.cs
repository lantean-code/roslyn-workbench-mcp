namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AddYieldTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpAddYieldCodeFixProvider";

    private static readonly IReadOnlyList<string> _diagnosticIds =
    [
        "CS0029",
        "CS0266",
    ];

    public AddYieldTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticIds)
    {
    }
}
