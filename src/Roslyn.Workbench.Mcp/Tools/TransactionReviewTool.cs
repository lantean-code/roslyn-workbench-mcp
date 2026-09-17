using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Creates a canonical, short-lived review receipt for the active transaction.
/// </summary>
internal sealed class TransactionReviewTool : ServerOwnedToolBase<TransactionReviewRequest, TransactionReviewData>
{
    private readonly ITransactionService _transactionService;
    private readonly ITransactionReceiptStore _receiptStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReviewTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol tool metadata.</param>
    /// <param name="requestBinder">The binder that validates and converts tool arguments.</param>
    /// <param name="transactionService">The service owning Workspace transaction review.</param>
    /// <param name="receiptStore">The process-local receipt store.</param>
    public TransactionReviewTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService,
        ITransactionReceiptStore receiptStore)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            ServerOwnedToolRegistration.TransactionReviewName,
            "Transaction Review",
            "Reviews the exact validated persistence set and returns the receipt required to commit it.",
            readOnly: true,
            destructive: false)
    {
        _transactionService = transactionService;
        _receiptStore = receiptStore;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionReviewData>> ExecuteAsync(
        TransactionReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.ReviewAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.ExpectedSnapshot,
            request.Document,
            request.IncludeDiff,
            request.ContextLines,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, CreateData);
    }

    private TransactionReviewData CreateData(TransactionReviewOutcome review)
    {
        var receipt = _receiptStore.CreateOrGet(review);
        var projection = TransactionMutationProvenanceMapper.Create(review.Provenance);

        return new TransactionReviewData
        {
            ReceiptId = receipt.ReceiptId,
            ExpiresAt = receipt.ExpiresAt,
            Algorithm = review.Identity.Algorithm,
            ChangeSetDigest = review.Identity.ChangeSetDigest,
            WorkspaceId = review.Identity.WorkspaceId,
            WorkspaceEpoch = review.Identity.WorkspaceEpoch,
            SnapshotId = review.Identity.SnapshotId,
            TransactionRevision = review.Identity.TransactionRevision,
            AddedDocumentCount = review.Documents.Count(static document => document.Operation == WorkspaceFileOperation.Create),
            ModifiedDocumentCount = review.Documents.Count(static document => document.Operation == WorkspaceFileOperation.Replace),
            DeletedDocumentCount = review.Documents.Count(static document => document.Operation == WorkspaceFileOperation.Delete),
            Transaction = review.Transaction,
            Documents = review.Documents,
            Provenance = projection.Provenance,
            Providers = projection.Providers,
            Validations = review.Validations,
            Diff = review.Diff,
            Continuation = ToolContinuation.CallTool(
                ServerOwnedToolRegistration.TransactionCommitName,
                "Call transaction-commit with this receiptId and the returned snapshot; the Host will request one-use approval."),
        };
    }
}
