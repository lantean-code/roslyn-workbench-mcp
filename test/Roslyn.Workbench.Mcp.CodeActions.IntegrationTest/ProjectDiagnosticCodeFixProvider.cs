using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

#pragma warning disable CA1812 // The provider fixture is instantiated by Roslyn MEF composition.
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ProjectDiagnosticCodeFixProvider))]
internal sealed class ProjectDiagnosticCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [ProjectDiagnosticAnalyzer._diagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var action = CodeAction.Create(
            "Apply project analyzer fix",
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(context.Document);
            },
            "ProjectAnalyzerFix");

        context.RegisterCodeFix(action, context.Diagnostics);
        return Task.CompletedTask;
    }
}
#pragma warning restore CA1812
