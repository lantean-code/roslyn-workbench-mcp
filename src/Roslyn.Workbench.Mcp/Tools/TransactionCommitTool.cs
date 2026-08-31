using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Commits the active workspace transaction to disk.
/// </summary>
internal sealed class TransactionCommitTool : ServerOwnedToolBase<TransactionCommitRequest, TransactionCommitData>
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCommitTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="transactionService">The service that owns transaction state and operations.</param>
    public TransactionCommitTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.TransactionCommitName,
            title: "Transaction Commit",
            description: "Commits the current staged transaction to disk.",
            readOnly: false,
            destructive: true)
    {
        _transactionService = transactionService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionCommitData>> ExecuteAsync(
        TransactionCommitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.CommitAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.ExpectedSnapshot,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionCommitData
        {
            Committed = data.Committed,
            Transaction = data.Transaction,
        });
    }
}
