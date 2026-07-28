using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

#pragma warning disable RS1001 // The test fixture is supplied directly as a project analyser rather than exported.
#pragma warning disable RS1036 // The test-only analyser does not ship and does not require banned API enforcement.
#pragma warning disable RS1038 // The analyser intentionally shares the integration-test assembly with its consuming scenario.
#pragma warning disable RS1041 // The analyser is a test fixture rather than a separately targeted compiler extension.
#pragma warning disable RS2008 // Test-only diagnostics do not require release tracking.
#pragma warning disable RS1007 // Test-only diagnostic text does not require localisation.
#pragma warning disable RS1015 // Test-only diagnostics do not require a shipped release file.
#pragma warning disable RS1028 // The integration fixture intentionally uses the test project's Roslyn references.
internal sealed class ProjectDiagnosticAnalyzer : DiagnosticAnalyzer
{
    internal const string _diagnosticId = "PROJECT9000";

    private static readonly DiagnosticDescriptor _descriptor = new(
        _diagnosticId,
        _diagnosticId,
        _diagnosticId,
        "Category",
        Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [_descriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(static syntaxTreeContext =>
        {
            syntaxTreeContext.ReportDiagnostic(Diagnostic.Create(
                _descriptor,
                syntaxTreeContext.Tree.GetLocation(new TextSpan(0, 1))));
        });
    }
}
#pragma warning restore RS1001
#pragma warning restore RS1036
#pragma warning restore RS1038
#pragma warning restore RS1041
#pragma warning restore RS2008
#pragma warning restore RS1007
#pragma warning restore RS1015
#pragma warning restore RS1028
