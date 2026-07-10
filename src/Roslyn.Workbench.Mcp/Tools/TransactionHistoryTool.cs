using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionHistoryTool : ServerOwnedToolBase<TransactionHistoryRequest, TransactionHistoryData>
{
    private readonly ITransactionService _transactionService;

    public TransactionHistoryTool(
        IOptions<StartupOptions> startupOptions,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            name: "transaction-history",
            title: "Transaction History",
            description: "Moves backward or forward through staged transaction history.",
            readOnly: false,
            destructive: true)
    {
        _transactionService = transactionService;
    }

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
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionHistoryData
        {
            Transaction = data.Transaction,
        });
    }
}
