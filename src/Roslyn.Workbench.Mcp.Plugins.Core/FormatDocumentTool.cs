using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class FormatDocumentTool : MutationToolHandler<FormatDocumentRequest, MutationProposal>
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

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(FormatDocumentRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return ExecuteFormatDocumentAsync(request, context, cancellationToken);
    }

    private static async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteFormatDocumentAsync(FormatDocumentRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documentResolution = ToolExecutionHelpers.ResolveDocument<MutationProposal>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var snapshotRejection = ToolExecutionHelpers.ValidateSnapshot<MutationProposal>(context, request.ExpectedSnapshot);
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
            return PluginExecutionResult<MutationProposal>.NoChange();
        }

        return PluginExecutionResult<MutationProposal>.Success(new MutationProposal
        {
            CandidateSolution = formattedDocument.Project.Solution,
            Summary = $"Format '{document.Name}'.",
        });
    }
}
