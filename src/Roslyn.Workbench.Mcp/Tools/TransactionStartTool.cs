using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionStartTool : ServerOwnedToolBase<TransactionStartRequest, TransactionStartData>
{
    private readonly ITransactionService _transactionService;

    public TransactionStartTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.TransactionStartName,
            title: "Transaction Start",
            description: "Starts a new staged transaction. Check workspace-status first and do not mutate a workspace that is or may be in use elsewhere unless mutation ownership has been coordinated.",
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
