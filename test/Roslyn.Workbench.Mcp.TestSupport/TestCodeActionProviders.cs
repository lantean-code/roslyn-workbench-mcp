using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.TestSupport;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = "TestRefactoringProvider")]
public sealed class TestRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null)
        {
            return;
        }

        var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
        if (!node.ToString().Contains("StateHolder", StringComparison.Ordinal))
        {
            return;
        }

        var renameAction = CodeAction.Create(
            "Apply test refactoring",
            cancellationToken => ReplaceAsync(document, "string.Empty", "\"RefactoredValue\"", cancellationToken),
            "TestRefactoring.Apply");
        var parameterisedAction = new ParameterisedTestCodeAction(document);
        var hiddenAction = CodeAction.Create(
            "Extract method test refactoring",
            cancellationToken => ReplaceAsync(document, "string.Empty", "\"HiddenValue\"", cancellationToken),
            "TestRefactoring.Hidden");
        var unsupportedAction = new UnsupportedOptionsTestCodeAction(document);
        var retainAction = CodeAction.Create(
            "Retain test state",
            cancellationToken => ReplaceAsync(document, "private set;", "private init;", cancellationToken),
            "TestRefactoring.Retain");
        var lateUnsupportedAction = new UnsupportedTestCodeAction(document);
        var groupAction = CodeAction.Create(
            "Test refactoring group",
            [renameAction, parameterisedAction, hiddenAction, unsupportedAction, retainAction, lateUnsupportedAction],
            isInlinable: true);

        context.RegisterRefactoring(groupAction);
    }

    private static async Task<Document> ReplaceAsync(Document document, string oldText, string newText, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        var source = text.ToString();
        var index = source.IndexOf(oldText, StringComparison.Ordinal);
        var updated = index < 0
            ? source
            : source.Remove(index, oldText.Length).Insert(index, newText);

        return document.WithText(SourceText.From(updated));
    }

    private sealed class UnsupportedTestCodeAction : CodeAction
    {
        private readonly Document _document;

        public UnsupportedTestCodeAction(Document document)
        {
            _document = document;
        }

        public override string Title => "Unsupported test refactoring";

        public override string EquivalenceKey => "TestRefactoring.Unsupported";

        protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken)
        {
            var text = await _document.GetTextAsync(cancellationToken);
            var updatedDocument = _document.WithText(SourceText.From(text.ToString() + Environment.NewLine + "public sealed class UnsupportedMarker { }"));
            var updatedSolution = updatedDocument.Project.Solution;
            return
            [
                new ApplyChangesOperation(updatedSolution),
                new ApplyChangesOperation(updatedSolution),
            ];
        }
    }

    private sealed class UnsupportedOptionsTestCodeAction : CodeActionWithOptions
    {
        private readonly Document _document;

        public UnsupportedOptionsTestCodeAction(Document document)
        {
            _document = document;
        }

        public override string Title => "Option gathering test refactoring";

        public override string EquivalenceKey => "TestRefactoring.OptionGathering";

        public override object? GetOptions(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            return new ParameterisedOptions
            {
                Replacement = "\"UnsupportedValue\"",
            };
        }

        protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(object? options, CancellationToken cancellationToken)
        {
            var parameterisedOptions = options as ParameterisedOptions ?? new ParameterisedOptions
            {
                Replacement = "\"UnsupportedValue\"",
            };
            var updatedDocument = await ReplaceAsync(_document, "string.Empty", parameterisedOptions.Replacement, cancellationToken);
            return [new ApplyChangesOperation(updatedDocument.Project.Solution)];
        }

        private sealed record ParameterisedOptions
        {
            public string Replacement { get; init; } = string.Empty;
        }
    }

    private sealed class ParameterisedTestCodeAction : CodeActionWithOptions
    {
        private readonly Document _document;

        public ParameterisedTestCodeAction(Document document)
        {
            _document = document;
        }

        public override string Title => "Change signature test refactoring";

        public override string EquivalenceKey => "TestRefactoring.Parameterised";

        public override object? GetOptions(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            return new ParameterisedOptions
            {
                Replacement = "\"ParameterisedValue\"",
            };
        }

        protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(object? options, CancellationToken cancellationToken)
        {
            var parameterisedOptions = options as ParameterisedOptions ?? new ParameterisedOptions
            {
                Replacement = "\"ParameterisedValue\"",
            };
            var updatedDocument = await ReplaceAsync(_document, "string.Empty", parameterisedOptions.Replacement, cancellationToken);
            return [new ApplyChangesOperation(updatedDocument.Project.Solution)];
        }

        private sealed record ParameterisedOptions
        {
            public string Replacement { get; init; } = string.Empty;
        }
    }
}

[ExportCodeFixProvider(LanguageNames.CSharp, Name = "TestCodeFixProvider")]
public sealed class TestCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["CS0219"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return FixAllProvider.Create(static async (fixAllContext, document, diagnostics) =>
        {
            var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken);
            if (root is null || diagnostics.IsDefaultOrEmpty)
            {
                return document;
            }

            var declarations = diagnostics
                .Select(diagnostic => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault())
                .Where(static declaration => declaration is not null)
                .Distinct()
                .Cast<LocalDeclarationStatementSyntax>()
                .ToArray();
            if (declarations.Length == 0)
            {
                return document;
            }

            var replacements = declarations.ToDictionary(
                static declaration => declaration,
                static declaration => SyntaxFactory.ParseStatement("_ = 42;").WithTriviaFrom(declaration));
            var updatedRoot = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
            return document.WithSyntaxRoot(updatedRoot);
        });
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null)
        {
            return;
        }

        var declaration = root.FindNode(context.Span, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
        if (declaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Apply test code fix",
                cancellationToken => ReplaceDeclarationAsync(document, declaration, cancellationToken),
                "TestCodeFix.Apply"),
            context.Diagnostics);
    }

    private static async Task<Document> ReplaceDeclarationAsync(Document document, LocalDeclarationStatementSyntax declaration, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        var updated = text.ToString().Replace(declaration.ToString(), "_ = 42;", StringComparison.Ordinal);
        return document.WithText(SourceText.From(updated));
    }
}
