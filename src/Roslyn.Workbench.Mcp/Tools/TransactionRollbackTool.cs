using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Discards the active workspace transaction and its staged changes.
/// </summary>
internal sealed class TransactionRollbackTool : ServerOwnedToolBase<TransactionRollbackRequest, TransactionRollbackData>
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionRollbackTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="transactionService">The service that owns transaction state and operations.</param>
    public TransactionRollbackTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.TransactionRollbackName,
            title: "Transaction Rollback",
            description: "Rolls back the current staged transaction.",
            readOnly: false,
            destructive: true)
    {
        _transactionService = transactionService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionRollbackData>> ExecuteAsync(
        TransactionRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.RollbackAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionRollbackData
        {
            State = data.State,
        });
    }
}
