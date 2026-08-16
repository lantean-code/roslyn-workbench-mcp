using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal sealed class ChangingFixAllCodeFixProvider : CodeFixProvider
{
    private int _fixAllApplicationCount;

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["CS0219"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return FixAllProvider.Create(ApplyFixAllAsync);
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var declaration = root?
            .FindNode(context.Span, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<LocalDeclarationStatementSyntax>()
            .FirstOrDefault();

        if (declaration is null)
        {
            return;
        }

        var action = CodeAction.Create(
            "Apply changing test code fix",
            cancellationToken => ReplaceDeclarationAsync(context.Document, declaration, "42;", cancellationToken),
            "ChangingTestCodeFix.Apply");

        context.RegisterCodeFix(action, context.Diagnostics);
    }

    private async Task<Document?> ApplyFixAllAsync(
        FixAllContext fixAllContext,
        Document document,
        ImmutableArray<Diagnostic> diagnostics)
    {
        var applicationCount = Interlocked.Increment(ref _fixAllApplicationCount);
        var replacement = applicationCount == 1 ? "42;" : "43;";
        var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken);
        if (root is null || diagnostics.IsDefaultOrEmpty)
        {
            return document;
        }

        var declarations = new HashSet<LocalDeclarationStatementSyntax>();
        foreach (var diagnostic in diagnostics)
        {
            var diagnosticNode = root.FindNode(
                diagnostic.Location.SourceSpan,
                getInnermostNodeForTie: true);

            var declaration = diagnosticNode
                .AncestorsAndSelf()
                .OfType<LocalDeclarationStatementSyntax>()
                .FirstOrDefault();

            if (declaration is not null)
            {
                declarations.Add(declaration);
            }
        }

        var replacements = declarations.ToDictionary(
            static declaration => declaration,
            declaration => SyntaxFactory.ParseStatement(replacement).WithTriviaFrom(declaration));

        var updatedRoot = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        return document.WithSyntaxRoot(updatedRoot);
    }

    private static async Task<Document> ReplaceDeclarationAsync(
        Document document,
        LocalDeclarationStatementSyntax declaration,
        string replacement,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        var updated = text.ToString().Replace(declaration.ToString(), replacement, StringComparison.Ordinal);
        return document.WithText(SourceText.From(updated));
    }
}
