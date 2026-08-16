using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal sealed class EncodingFixAllCodeFixProvider : CodeFixProvider
{
    private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["CS0219"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return FixAllProvider.Create(ApplyFixAllAsync);
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var action = CodeAction.Create(
            "Apply encoding test code fix",
            cancellationToken => ChangeEncodingAsync(context.Document, cancellationToken),
            "EncodingTestCodeFix.Apply");

        context.RegisterCodeFix(action, context.Diagnostics);
        return Task.CompletedTask;
    }

    private static async Task<Document?> ApplyFixAllAsync(
        FixAllContext fixAllContext,
        Document document,
        ImmutableArray<Diagnostic> _)
    {
        var changedDocument = await ChangeEncodingAsync(document, fixAllContext.CancellationToken);
        return changedDocument;
    }

    private static async Task<Document> ChangeEncodingAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        var updatedText = SourceText.From(text.ToString(), _encoding, text.ChecksumAlgorithm);
        return document.WithText(updatedText);
    }
}
