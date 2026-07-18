using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionCommitTool : ServerOwnedToolBase<TransactionCommitRequest, TransactionCommitData>
{
    private readonly ITransactionService _transactionService;

    public TransactionCommitTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.TransactionCommitName,
            title: "Transaction Commit",
            description: "Commits the current staged transaction to disk.",
            readOnly: false,
            destructive: true)
    {
        _transactionService = transactionService;
    }

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
