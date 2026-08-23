using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ThrowingCodeFixProvider))]
internal sealed class ThrowingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["CS0219"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(new ThrowingCodeAction(), context.Diagnostics);
        return Task.CompletedTask;
    }

    private sealed class ThrowingCodeAction : CodeAction
    {
        public override string EquivalenceKey => "ThrowingCodeFix.Action";

        public override string Title
        {
            get
            {
                throw new InvalidOperationException("Controlled provider action failure.");
            }
        }

        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<CodeActionOperation>>([]);
        }
    }
}
