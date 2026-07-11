using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class WorkspaceRuntime : IWorkspaceRuntime
{
    private readonly IToolExecutionContextFactory _coordinator;
    private readonly ICodeActionExecutionContextFactory _codeActionContextFactory;
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;
    private readonly ITransactionService _transactionService;

    internal WorkspaceRuntime(
        IToolExecutionContextFactory coordinator,
        ICodeActionExecutionContextFactory codeActionContextFactory,
        IWorkspaceLifecycleService workspaceLifecycleService,
        ITransactionService transactionService)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _codeActionContextFactory = codeActionContextFactory ?? throw new ArgumentNullException(nameof(codeActionContextFactory));
        _workspaceLifecycleService = workspaceLifecycleService ?? throw new ArgumentNullException(nameof(workspaceLifecycleService));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
    }

    internal IWorkspaceLifecycleService WorkspaceLifecycleService
    {
        get { return _workspaceLifecycleService; }
    }

    internal ITransactionService TransactionService
    {
        get { return _transactionService; }
    }

    internal ICodeActionExecutionContextFactory CodeActionContextFactory
    {
        get { return _codeActionContextFactory; }
    }

    public PluginMutationExecutionLease CreateMutationContext(WorkspaceBoundRequest request, CancellationToken cancellationToken)
    {
        return _coordinator.CreateMutationContext(request, cancellationToken);
    }

    public ToolExecutionContextLease<IQueryContext> CreateQueryContext(WorkspaceBoundRequest request, CancellationToken cancellationToken)
    {
        return _coordinator.CreateQueryContext(request, cancellationToken);
    }

    public ValueTask<ToolResult<WorkspaceOpenData>> OpenAsync(WorkspaceOpenRequest request, CancellationToken cancellationToken)
    {
        return OpenCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<WorkspaceListData>> ListAsync(WorkspaceListRequest request, CancellationToken cancellationToken)
    {
        return ListCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<WorkspaceCloseData>> CloseAsync(WorkspaceCloseRequest request, CancellationToken cancellationToken)
    {
        return CloseCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<WorkspaceStatusData>> GetStatusAsync(WorkspaceStatusRequest request, CancellationToken cancellationToken)
    {
        return GetStatusCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<WorkspaceReloadData>> ReloadAsync(WorkspaceReloadRequest request, CancellationToken cancellationToken)
    {
        return ReloadCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<TransactionStartData>> StartTransactionAsync(TransactionStartRequest request, CancellationToken cancellationToken)
    {
        return StartTransactionCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<TransactionPreviewData>> PreviewTransactionAsync(TransactionPreviewRequest request, CancellationToken cancellationToken)
    {
        return PreviewTransactionCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<TransactionHistoryData>> MoveTransactionHistoryAsync(TransactionHistoryRequest request, CancellationToken cancellationToken)
    {
        return MoveTransactionHistoryCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<TransactionCommitData>> CommitTransactionAsync(TransactionCommitRequest request, CancellationToken cancellationToken)
    {
        return CommitTransactionCoreAsync(request, cancellationToken);
    }

    public ValueTask<ToolResult<TransactionRollbackData>> RollbackTransactionAsync(TransactionRollbackRequest request, CancellationToken cancellationToken)
    {
        return RollbackTransactionCoreAsync(request, cancellationToken);
    }

    private async ValueTask<ToolResult<WorkspaceOpenData>> OpenCoreAsync(WorkspaceOpenRequest request, CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.OpenAsync(
            request.Path,
            request.Alias,
            request.WorkspaceRoot,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceOpenData
        {
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
        });
    }

    private async ValueTask<ToolResult<WorkspaceListData>> ListCoreAsync(WorkspaceListRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        var result = await _workspaceLifecycleService.ListAsync(cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceListData
        {
            Workspaces = data.Workspaces,
            TransactionOwnerWorkspaceId = data.TransactionOwnerWorkspaceId,
        });
    }

    private async ValueTask<ToolResult<WorkspaceCloseData>> CloseCoreAsync(WorkspaceCloseRequest request, CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.CloseAsync(request.Workspace?.WorkspaceId, request.Workspace?.Alias, request.Workspace?.Path, cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceCloseData
        {
            ClosedPath = data.ClosedPath,
        });
    }

    private async ValueTask<ToolResult<WorkspaceStatusData>> GetStatusCoreAsync(WorkspaceStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.GetStatusAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.Detail,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceStatusData
        {
            State = data.State,
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
            Transaction = data.Transaction,
            ReloadRequired = data.ReloadRequired,
            Instances = data.Instances,
        });
    }

    private async ValueTask<ToolResult<WorkspaceReloadData>> ReloadCoreAsync(WorkspaceReloadRequest request, CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.ReloadAsync(request.Workspace?.WorkspaceId, request.Workspace?.Alias, request.Workspace?.Path, cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceReloadData
        {
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
        });
    }

    private async ValueTask<ToolResult<TransactionStartData>> StartTransactionCoreAsync(TransactionStartRequest request, CancellationToken cancellationToken)
    {
        var result = await _transactionService.StartAsync(request.Workspace?.WorkspaceId, request.Workspace?.Alias, request.Workspace?.Path, cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionStartData
        {
            Transaction = data.Transaction,
        });
    }

    private async ValueTask<ToolResult<TransactionPreviewData>> PreviewTransactionCoreAsync(TransactionPreviewRequest request, CancellationToken cancellationToken)
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

    private async ValueTask<ToolResult<TransactionHistoryData>> MoveTransactionHistoryCoreAsync(TransactionHistoryRequest request, CancellationToken cancellationToken)
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

    private async ValueTask<ToolResult<TransactionCommitData>> CommitTransactionCoreAsync(TransactionCommitRequest request, CancellationToken cancellationToken)
    {
        var result = await _transactionService.CommitAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.ExpectedSnapshot,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionCommitData
        {
            Committed = data.Committed,
            Transaction = data.Transaction,
        });
    }

    private async ValueTask<ToolResult<TransactionRollbackData>> RollbackTransactionCoreAsync(TransactionRollbackRequest request, CancellationToken cancellationToken)
    {
        var result = await _transactionService.RollbackAsync(request.Workspace?.WorkspaceId, request.Workspace?.Alias, request.Workspace?.Path, cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new TransactionRollbackData
        {
            State = data.State,
        });
    }
}
