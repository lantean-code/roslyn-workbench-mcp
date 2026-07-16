using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class TransactionPreviewTool : ServerOwnedToolBase<TransactionPreviewRequest, TransactionPreviewData>
{
    private readonly ITransactionService _transactionService;

    public TransactionPreviewTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.TransactionPreviewName,
            title: "Transaction Preview",
            description: "Previews the current staged transaction.",
            readOnly: true,
            destructive: false)
    {
        _transactionService = transactionService;
    }

    protected override async ValueTask<ToolResult<TransactionPreviewData>> ExecuteAsync(
        TransactionPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.PreviewAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.Document,
            request.IncludeDiff,
            request.ContextLines,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionPreviewData
        {
            Transaction = data.Transaction,
            Documents = data.Documents,
            Diff = data.Diff,
        });
    }
}
