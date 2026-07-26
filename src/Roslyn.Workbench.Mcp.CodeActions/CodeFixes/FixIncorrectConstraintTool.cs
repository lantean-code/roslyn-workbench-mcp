namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class FixIncorrectConstraintTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixIncorrectConstraint.CSharpFixIncorrectConstraintCodeFixProvider";

    private static readonly IReadOnlyList<string> _diagnosticIds =
    [
        "CS9010",
        "CS9011",
    ];

    public FixIncorrectConstraintTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticIds)
    {
    }
}
