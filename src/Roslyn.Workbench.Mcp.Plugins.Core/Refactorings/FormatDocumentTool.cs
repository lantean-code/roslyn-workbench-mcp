using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class FormatDocumentTool : MutationToolHandler<FormatDocumentRequest>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "format-document",
        Title = "Format Document",
        Description = "Stages Roslyn formatting for one document or one selected range.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new FormatDocumentTool());
    }

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
            formattedDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var span = new TextSpan(request.Range.Start, request.Range.Length);
            formattedDocument = await Formatter.FormatAsync(document, span, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var originalText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var formattedText = await formattedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(originalText.ToString(), formattedText.ToString(), StringComparison.Ordinal))
        {
            return PluginExecutionResult<MutationCandidate>.NoChange();
        }

        return PluginExecutionResult<MutationCandidate>.Success(new MutationCandidate
        {
            CandidateSolution = formattedDocument.Project.Solution,
            Summary = $"Format '{document.Name}'.",
        });
    }
}
