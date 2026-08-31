using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Starts an isolated mutation transaction for a loaded workspace.
/// </summary>
internal sealed class TransactionStartTool : ServerOwnedToolBase<TransactionStartRequest, TransactionStartData>
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionStartTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="transactionService">The service that owns transaction state and operations.</param>
    public TransactionStartTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.TransactionStartName,
            title: "Transaction Start",
            description: "Starts a new staged transaction. Check workspace-status first and do not mutate a workspace that is or may be in use elsewhere unless mutation ownership has been coordinated.",
            readOnly: false,
            destructive: false)
    {
        _transactionService = transactionService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionStartData>> ExecuteAsync(
        TransactionStartRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.StartAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionStartData
        {
            Transaction = data.Transaction,
        });
    }
}
