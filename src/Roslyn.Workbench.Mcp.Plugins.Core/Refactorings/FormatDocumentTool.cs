namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

/// <summary>
/// Stages Roslyn formatting for one document or one selected range.
/// </summary>
[RoslynTool("format-document", "Format Document", "Stages Roslyn formatting for one document or one selected range.", Destructive = true)]
internal sealed class FormatDocumentTool : MutationToolHandler<FormatDocumentRequest>
{
    /// <inheritdoc/>
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
        var originalText = await document.GetTextAsync(cancellationToken);
        Document? formattedDocument;
        if (request.Range is null)
        {
            formattedDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        }
        else
        {
            if (request.Range.Start < 0
                || request.Range.Length < 0
                || request.Range.Start > originalText.Length - request.Range.Length)
            {
                return PluginExecutionResult.Rejected<MutationCandidate>(
                    "InvalidRequest",
                    "The range must identify a span within the selected document.");
            }

            var span = new TextSpan(request.Range.Start, request.Range.Length);
            formattedDocument = await Formatter.FormatAsync(document, span, cancellationToken: cancellationToken);
        }

        var formattedText = await formattedDocument.GetTextAsync(cancellationToken);
        if (originalText.ContentEquals(formattedText))
        {
            return PluginExecutionResult.NoChange<MutationCandidate>();
        }

        var candidate = new MutationCandidate
        {
            CandidateSolution = formattedDocument.Project.Solution,
            Summary = $"Format '{document.Name}'.",
        };

        return PluginExecutionResult.Success(candidate);
    }
}
