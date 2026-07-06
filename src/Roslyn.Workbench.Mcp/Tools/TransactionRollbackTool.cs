using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Transactions;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionRollbackTool : ServerOwnedToolBase<TransactionRollbackRequest, TransactionRollbackData>
{
    private readonly ITransactionService _transactionService;

    public TransactionRollbackTool(
        IOptions<StartupOptions> startupOptions,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            name: "transaction-rollback",
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
