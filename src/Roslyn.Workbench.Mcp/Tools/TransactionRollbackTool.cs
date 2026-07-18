using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionRollbackTool : ServerOwnedToolBase<TransactionRollbackRequest, TransactionRollbackData>
{
    private readonly ITransactionService _transactionService;

    public TransactionRollbackTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.TransactionRollbackName,
            title: "Transaction Rollback",
            description: "Rolls back the current staged transaction.",
            readOnly: false,
            destructive: true)
    {
        _transactionService = transactionService;
    }

    protected override async ValueTask<ToolResult<TransactionRollbackData>> ExecuteAsync(
        TransactionRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.RollbackAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionRollbackData
        {
            State = data.State,
        });
    }
}
