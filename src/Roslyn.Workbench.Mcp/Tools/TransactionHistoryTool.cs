using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Moves the active transaction backward or forward through its staged revisions.
/// </summary>
internal sealed class TransactionHistoryTool : ServerOwnedToolBase<TransactionHistoryRequest, TransactionHistoryData>
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionHistoryTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="transactionService">The service that owns transaction state and operations.</param>
    public TransactionHistoryTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.TransactionHistoryName,
            title: "Transaction History",
            description: "Undoes or redoes one staged revision within the active transaction, changing the in-memory solution without writing source files. Use this to revise staged work; use transaction-rollback to discard the entire transaction. Supply the current expectedSnapshot and use the returned snapshot for subsequent mutations. The returned transaction reports canUndo and canRedo; a move beyond available history is rejected. A conflicted transaction must be rolled back before history can change.",
            readOnly: false,
            destructive: true)
    {
        _transactionService = transactionService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionHistoryData>> ExecuteAsync(
        TransactionHistoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.MoveHistoryAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.Direction,
            request.ExpectedSnapshot,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionHistoryData
        {
            Transaction = data.Transaction,
        });
    }
}
