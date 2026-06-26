using ModelContextProtocol.Server;

using Roslyn.Workbench.Mcp.Contracts.Transactions;

using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp;

internal static class TransactionToolFactory
{
    public static IReadOnlyList<McpServerTool> Create(IWorkspaceCoordinator coordinator)
    {
        return
        [
            new ServerToolMcpServerTool<TransactionStartRequest, TransactionStartData>(
                "transaction-start",
                "Transaction Start",
                "Starts a new staged transaction.",
                readOnly: false,
                destructive: false,
                (request, _, cancellationToken) => coordinator.StartTransactionAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<TransactionPreviewRequest, TransactionPreviewData>(
                "transaction-preview",
                "Transaction Preview",
                "Previews the current staged transaction.",
                readOnly: true,
                destructive: false,
                (request, _, cancellationToken) => coordinator.PreviewTransactionAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<TransactionHistoryRequest, TransactionHistoryData>(
                "transaction-history",
                "Transaction History",
                "Moves backward or forward through staged transaction history.",
                readOnly: false,
                destructive: true,
                (request, _, cancellationToken) => coordinator.MoveTransactionHistoryAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<TransactionCommitRequest, TransactionCommitData>(
                "transaction-commit",
                "Transaction Commit",
                "Commits the current staged transaction to disk.",
                readOnly: false,
                destructive: true,
                (request, _, cancellationToken) => coordinator.CommitTransactionAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<TransactionRollbackRequest, TransactionRollbackData>(
                "transaction-rollback",
                "Transaction Rollback",
                "Rolls back the current staged transaction.",
                readOnly: false,
                destructive: true,
                (request, _, cancellationToken) => coordinator.RollbackTransactionAsync(request, cancellationToken)),
        ];
    }
}
