using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SiblingRootCodeFixProvider))]
internal sealed class SiblingRootCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["CS0219"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var firstAction = CodeAction.Create(
            "Apply sibling code fix",
            cancellationToken => ReplaceDeclarationAsync(context.Document, context.Span, "_ = 1;", cancellationToken),
            "SiblingCodeFix.Apply");

        var secondAction = CodeAction.Create(
            "Apply sibling code fix",
            cancellationToken => ReplaceDeclarationAsync(context.Document, context.Span, "_ = 2;", cancellationToken),
            "SiblingCodeFix.Apply");

        context.RegisterCodeFix(firstAction, context.Diagnostics);
        context.RegisterCodeFix(secondAction, context.Diagnostics);
        return Task.CompletedTask;
    }

    private static async Task<Document> ReplaceDeclarationAsync(
        Document document,
        TextSpan diagnosticSpan,
        string replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is null)
        {
            return document;
        }

        var declaration = root
            .FindNode(diagnosticSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<LocalDeclarationStatementSyntax>()
            .FirstOrDefault();

        if (declaration is null)
        {
            return document;
        }

        var replacementStatement = SyntaxFactory.ParseStatement(replacement).WithTriviaFrom(declaration);
        var updatedRoot = root.ReplaceNode(declaration, replacementStatement);
        return document.WithSyntaxRoot(updatedRoot);
    }
}
