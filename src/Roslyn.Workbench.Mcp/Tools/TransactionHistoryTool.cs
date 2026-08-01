using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionHistoryTool : ServerOwnedToolBase<TransactionHistoryRequest, TransactionHistoryData>
{
    private readonly ITransactionService _transactionService;

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
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionHistoryData
        {
            Transaction = data.Transaction,
        });
    }
}
