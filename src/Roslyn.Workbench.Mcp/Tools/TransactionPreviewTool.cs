using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Produces a document-level preview of changes staged in the active transaction.
/// </summary>
internal sealed class TransactionPreviewTool : ServerOwnedToolBase<TransactionPreviewRequest, TransactionPreviewData>
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPreviewTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="transactionService">The service that owns transaction state and operations.</param>
    public TransactionPreviewTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.TransactionPreviewName,
            title: "Transaction Preview",
            description: "Inspects the active transaction against its captured baseline before committing, returning transaction state, changed documents and mutation provenance. Available in mutation modes that do not require a review receipt; approval-required mode exposes transaction-review instead. includeDiff defaults to false; set it to true and supply document for a detailed diff of that document, with contextLines defaulting to 3 unchanged lines per hunk. The document selector controls the detailed diff, not the overall changed-document summary. This read-only preview does not authorise or persist changes. Inspect it, then use transaction-commit to write the staged changes or transaction-rollback to discard them.",
            readOnly: true,
            destructive: false)
    {
        _transactionService = transactionService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionPreviewData>> ExecuteAsync(
        TransactionPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.PreviewAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.Document,
            request.IncludeDiff,
            request.ContextLines,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data =>
        {
            var projection = TransactionMutationProvenanceMapper.Create(data.Provenance);
            return new TransactionPreviewData
            {
                Transaction = data.Transaction,
                Documents = data.Documents,
                Provenance = projection.Provenance,
                Providers = projection.Providers,
                Diff = data.Diff,
            };
        });
    }
}
