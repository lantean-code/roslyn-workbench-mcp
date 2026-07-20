namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

[RoslynTool("format-document", "Format Document", "Stages Roslyn formatting for one document or one selected range.", Destructive = true)]
internal sealed class FormatDocumentTool : MutationToolHandler<FormatDocumentRequest>
{
    protected override ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteCoreAsync(FormatDocumentRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ExecuteFormatDocumentAsync(request, context, cancellationToken);
    }

    private static async ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteFormatDocumentAsync(FormatDocumentRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<MutationCandidate>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<MutationCandidate>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var document = documentResolution.Value;
        Document? formattedDocument;
        if (request.Range is null)
        {
            formattedDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        }
        else
        {
            var span = new TextSpan(request.Range.Start, request.Range.Length);
            formattedDocument = await Formatter.FormatAsync(document, span, cancellationToken: cancellationToken);
        }

        var originalText = await document.GetTextAsync(cancellationToken);
        var formattedText = await formattedDocument.GetTextAsync(cancellationToken);
        if (originalText.ContentEquals(formattedText))
        {
            return PluginExecutionResult<MutationCandidate>.NoChange();
        }

        var candidate = new MutationCandidate
        {
            CandidateSolution = formattedDocument.Project.Solution,
            Summary = $"Format '{document.Name}'.",
        };

        return PluginExecutionResult<MutationCandidate>.Success(candidate);
    }
}
