namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class DeclareAsNullableTool : FixedCompilerCodeFixTool
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider";

    private static readonly IReadOnlyList<string> _diagnosticIds =
    [
        "CS8603",
        "CS8600",
        "CS8625",
        "CS8618",
    ];

    public DeclareAsNullableTool(ILocationCodeFixStager locationFixStager)
        : base(locationFixStager, _providerId, _diagnosticIds)
    {
    }
}
