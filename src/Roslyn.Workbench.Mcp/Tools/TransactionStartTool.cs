using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionStartTool : ServerOwnedToolBase<TransactionStartRequest, TransactionStartData>
{
    private readonly ITransactionService _transactionService;

    public TransactionStartTool(
        IOptions<StartupOptions> startupOptions,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            name: "transaction-start",
            title: "Transaction Start",
            description: "Starts a new staged transaction.",
            readOnly: false,
            destructive: false)
    {
        _transactionService = transactionService;
    }

    protected override async ValueTask<ToolResult<TransactionStartData>> ExecuteAsync(
        TransactionStartRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.StartAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionStartData
        {
            Transaction = data.Transaction,
        });
    }
}
