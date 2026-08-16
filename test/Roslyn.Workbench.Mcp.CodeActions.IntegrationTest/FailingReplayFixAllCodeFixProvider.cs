using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal sealed class FailingReplayFixAllCodeFixProvider : CodeFixProvider
{
    private readonly FailingReplayFixAllProvider _fixAllProvider = new();

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["CS0219"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return _fixAllProvider;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var action = CodeAction.Create(
            "Apply failing replay test code fix",
            cancellationToken => AppendMarkerAsync(context.Document, cancellationToken),
            "FailingReplayTestCodeFix.Apply");

        context.RegisterCodeFix(action, context.Diagnostics);
        return Task.CompletedTask;
    }

    private static async Task<Document> AppendMarkerAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        var marker = $"{Environment.NewLine}// Applied by the failing replay test.";
        return document.WithText(text.WithChanges(new TextChange(new TextSpan(text.Length, 0), marker)));
    }

    private sealed class FailingReplayFixAllProvider : FixAllProvider
    {
        private int _creationCount;

        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (Interlocked.Increment(ref _creationCount) > 1)
            {
                return new UnsupportedCodeAction();
            }

            var document = fixAllContext.Document
                ?? throw new InvalidOperationException("The test Fix All context does not contain an origin document.");
            var changedDocument = await AppendMarkerAsync(document, fixAllContext.CancellationToken);
            var changedSolution = changedDocument.Project.Solution;
            return CodeAction.Create(
                "Apply failing replay test Fix All",
                _ => Task.FromResult(changedSolution),
                "FailingReplayTestCodeFix.Apply");
        }
    }

    private sealed class UnsupportedCodeAction : CodeAction
    {
        public override string Title
        {
            get
            {
                return "Unsupported failing replay test Fix All";
            }
        }

        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(
            CancellationToken cancellationToken)
        {
            IEnumerable<CodeActionOperation> operations = [new UnsupportedOperation()];
            return Task.FromResult(operations);
        }
    }

    private sealed class UnsupportedOperation : CodeActionOperation
    {
    }
}
