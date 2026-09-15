using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Provides the shared tool-layer transition from an authorised commit request into the Workspace transaction service.
/// </summary>
/// <typeparam name="TRequest">The mode-specific commit request contract.</typeparam>
internal abstract class TransactionCommitToolBase<TRequest> : ServerOwnedToolBase<TRequest, TransactionCommitData>
    where TRequest : WorkspaceMutationRequest
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCommitToolBase{TRequest}"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol tool metadata.</param>
    /// <param name="requestBinder">The binder that validates and converts tool arguments.</param>
    /// <param name="transactionService">The protocol-neutral service that owns transaction operations.</param>
    /// <param name="description">The mode-specific tool description.</param>
    protected TransactionCommitToolBase(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService,
        string description)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            ServerOwnedToolRegistration.TransactionCommitName,
            "Transaction Commit",
            description,
            readOnly: false,
            destructive: true)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    /// Gets the protocol-neutral transaction service for mode-specific pre-commit validation.
    /// </summary>
    protected ITransactionService TransactionService => _transactionService;

    /// <summary>
    /// Commits an authorised request through the shared Workspace transaction pipeline and maps its result to the commit tool contract.
    /// </summary>
    /// <param name="request">The bound mode-specific commit request.</param>
    /// <param name="receiptAuthorisation">The optional exact-change receipt authorisation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The mapped transaction commit result.</returns>
    protected async ValueTask<ToolResult<TransactionCommitData>> CommitAuthorisedAsync(
        TRequest request,
        TransactionReceiptAuthorisation? receiptAuthorisation,
        CancellationToken cancellationToken)
    {
        var workspaceId = request.Workspace?.WorkspaceId;
        var alias = request.Workspace?.Alias;
        var path = request.Workspace?.Path;

        WorkspaceOperationResult<TransactionCommitOutcome> result;
        if (receiptAuthorisation is null)
        {
            result = await _transactionService.CommitAsync(
                workspaceId,
                alias,
                path,
                request.ExpectedSnapshot,
                cancellationToken);
        }
        else
        {
            result = await _transactionService.CommitAsync(
                workspaceId,
                alias,
                path,
                request.ExpectedSnapshot,
                receiptAuthorisation,
                cancellationToken);
        }

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionCommitData
        {
            Committed = data.Committed,
            Transaction = data.Transaction,
        });
    }

    /// <summary>
    /// Creates a rejected commit tool result with optional recovery guidance.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The user-facing failure message.</param>
    /// <param name="requiredAction">The optional action required to recover.</param>
    /// <returns>The rejected commit result.</returns>
    protected static ToolResult<TransactionCommitData> CreateCommitFailure(
        string code,
        string message,
        RequiredAction? requiredAction)
    {
        var error = new ToolError
        {
            Code = code,
            Message = message,
        };

        return ToolResult.Rejected<TransactionCommitData>(error, requiredAction);
    }
}
