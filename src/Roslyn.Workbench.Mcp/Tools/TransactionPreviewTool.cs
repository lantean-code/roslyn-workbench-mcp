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
            description: "Previews the current staged transaction.",
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

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionPreviewData
        {
            Transaction = data.Transaction,
            Documents = data.Documents,
            Diff = data.Diff,
        });
    }
}
