using System.Collections.ObjectModel;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Requests one-use approval and commits the exact change represented by a review receipt.
/// </summary>
internal sealed class TransactionReceiptCommitTool : TransactionCommitToolBase<TransactionReceiptCommitRequest>
{
    private const string _approve = "approve-this-receipt";
    private const string _doNotCommit = "do-not-commit";
    private static readonly ReadOnlyCollection<UserInteractionChoice> _choices = CreateChoices();

    private readonly ITransactionReceiptStore _receiptStore;
    private readonly IMcpUserInteractionServiceFactory _interactionServiceFactory;
    private readonly OperationalPolicy _operationalPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReceiptCommitTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol tool metadata.</param>
    /// <param name="requestBinder">The binder that validates and converts tool arguments.</param>
    /// <param name="transactionService">The service owning Workspace commit operations.</param>
    /// <param name="receiptStore">The process-local receipt store.</param>
    /// <param name="interactionServiceFactory">The factory isolating MCP elicitation.</param>
    /// <param name="operationalPolicy">The effective immutable operational policy.</param>
    public TransactionReceiptCommitTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService,
        ITransactionReceiptStore receiptStore,
        IMcpUserInteractionServiceFactory interactionServiceFactory,
        OperationalPolicy operationalPolicy)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            transactionService,
            "Requests one-use approval for a transaction-review receipt, then commits only that exact change.")
    {
        _receiptStore = receiptStore;
        _interactionServiceFactory = interactionServiceFactory;
        _operationalPolicy = operationalPolicy;
    }

    /// <inheritdoc/>
    protected override ValueTask<ToolResult<TransactionCommitData>> ExecuteAsync(
        TransactionReceiptCommitRequest request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(CreateUnavailableFailure());
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionCommitData>> ExecuteAsync(
        TransactionReceiptCommitRequest request,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        var resolution = _receiptStore.Resolve(request.ReceiptId);
        if (!resolution.IsAvailable)
        {
            return CreateReceiptUnavailableFailure(resolution.Status);
        }

        var receipt = resolution.Receipt;
        var validation = await TransactionService.ValidateReceiptAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.ExpectedSnapshot,
            receipt.Review.Identity,
            cancellationToken);

        if (!validation.HasData)
        {
            return WorkspaceToolResultMapper.Map(validation, static _ => new TransactionCommitData());
        }

        if (_operationalPolicy.CompilerValidationRequired)
        {
            var compilerValidationFailure = await ValidateCompilerImpactAsync(
                request,
                cancellationToken);

            if (compilerValidationFailure is not null)
            {
                return compilerValidationFailure;
            }
        }

        var interactionService = _interactionServiceFactory.Create(requestContext.Server);
        var approval = await interactionService.RequestAsync(CreateApprovalRequest(receipt), cancellationToken);
        var approvalFailure = ValidateApproval(approval);
        if (approvalFailure is not null)
        {
            return approvalFailure;
        }

        var consumption = _receiptStore.Consume(request.ReceiptId, receipt.Review.Identity);
        if (!consumption.IsAvailable)
        {
            return CreateReceiptUnavailableFailure(consumption.Status);
        }

        var authorisation = new TransactionReceiptAuthorisation
        {
            Identity = receipt.Review.Identity,
        };

        return await CommitAuthorisedAsync(
            request,
            authorisation,
            cancellationToken);
    }

    private static UserInteractionRequest CreateApprovalRequest(TransactionReceipt receipt)
    {
        var review = receipt.Review;
        var addedCount = review.Documents.Count(static document => document.Operation == WorkspaceFileOperation.Create);
        var modifiedCount = review.Documents.Count(static document => document.Operation == WorkspaceFileOperation.Replace);
        var deletedCount = review.Documents.Count(static document => document.Operation == WorkspaceFileOperation.Delete);

        return new UserInteractionRequest
        {
            Message = $"Commit reviewed change {review.Identity.ChangeSetDigest} affecting {review.Documents.Count} file(s): {addedCount} added, {modifiedCount} modified, {deletedCount} deleted?",
            Title = "Exact transaction receipt approval",
            Description = $"Approve only receipt {receipt.ReceiptId} for transaction revision {review.Identity.TransactionRevision}. The receipt is one-use and expires at {receipt.ExpiresAt:O}.",
            Choices = _choices,
        };
    }

    private static ToolResult<TransactionCommitData>? ValidateApproval(UserInteractionResult approval)
    {
        if (!approval.IsAccepted)
        {
            return approval.Outcome switch
            {
                UserInteractionOutcome.Declined or UserInteractionOutcome.Cancelled => CreateNotApprovedFailure(),
                UserInteractionOutcome.Unavailable or UserInteractionOutcome.Failed => CreateUnavailableFailure(),
                _ => CreateInvalidResponseFailure(),
            };
        }

        return approval.SelectedValue switch
        {
            _approve => null,
            _doNotCommit => CreateNotApprovedFailure(),
            _ => CreateInvalidResponseFailure(),
        };
    }

    private static ReadOnlyCollection<UserInteractionChoice> CreateChoices()
    {
        return Array.AsReadOnly(new UserInteractionChoice[]
        {
            new() { Value = _approve, Title = "Yes, commit this reviewed change" },
            new() { Value = _doNotCommit, Title = "No, don't commit" },
        });
    }

    private static ToolResult<TransactionCommitData> CreateReceiptUnavailableFailure(TransactionReceiptResolutionStatus status)
    {
        var reason = status == TransactionReceiptResolutionStatus.Expired ? "expired" : "is missing or has already been used";
        return CreateCommitFailure(
            "TransactionReceiptUnavailable",
            $"No files were persisted because the transaction receipt {reason}. Run transaction-review again and approve the new receipt.",
            RequiredAction.ReviewTransaction);
    }

    private static ToolResult<TransactionCommitData> CreateNotApprovedFailure()
    {
        return CreateCommitFailure(
            "TransactionCommitNotApproved",
            "No files were persisted because this exact transaction receipt was not approved. The transaction and receipt remain active for a deliberate retry.",
            requiredAction: null);
    }

    private static ToolResult<TransactionCommitData> CreateUnavailableFailure()
    {
        return CreateCommitFailure(
            "ApprovalUnavailable",
            "No files were persisted because the connected MCP client could not complete receipt approval. The transaction and receipt remain active. Enable interactive MCP requests before a deliberate retry.",
            RequiredAction.Retry);
    }

    private static ToolResult<TransactionCommitData> CreateInvalidResponseFailure()
    {
        return CreateCommitFailure(
            "InvalidApprovalResponse",
            "No files were persisted because the client returned an unsupported receipt-approval choice. The transaction and receipt remain active.",
            RequiredAction.Retry);
    }
}
