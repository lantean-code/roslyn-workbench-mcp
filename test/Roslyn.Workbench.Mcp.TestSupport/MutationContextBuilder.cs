using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class MutationContextBuilder
{
    private Solution _currentSolution = new AdhocWorkspace().CurrentSolution;
    private WorkspaceIdentity _workspaceIdentity = default!;
    private int? _transactionRevision;
    private int _defaultMaxResults = 100;
    private IWorkspaceResolver? _resolver;
    private IToolExecutionServices _toolExecutionServices = new ToolExecutionServicesBuilder().Build();
    private Func<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken, ValueTask<PluginExecutionResult<MutationData>>>? _stageAsync;
    private Func<StageCodeActionRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>>? _stageCodeActionAsync;
    private Func<ReplayCodeActionRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>>? _stageReplayCodeActionAsync;
    private Func<StageCodeFixRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>>? _stageCodeFixAsync;
    private Func<StageFixAllRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>>? _stageFixAllAsync;
    private Func<ScopedCodeFixRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>>? _stageScopedCodeFixAsync;
    private Func<LocationCodeFixRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>>? _stageLocationCodeFixAsync;

    public MutationContextBuilder WithCurrentSolution(Solution currentSolution)
    {
        _currentSolution = currentSolution ?? throw new ArgumentNullException(nameof(currentSolution));
        return this;
    }

    public MutationContextBuilder WithWorkspaceIdentity(WorkspaceIdentity workspaceIdentity)
    {
        _workspaceIdentity = workspaceIdentity;
        return this;
    }

    public MutationContextBuilder WithTransactionRevision(int? transactionRevision)
    {
        _transactionRevision = transactionRevision;
        return this;
    }

    public MutationContextBuilder WithDefaultMaxResults(int defaultMaxResults)
    {
        _defaultMaxResults = defaultMaxResults;
        return this;
    }

    public MutationContextBuilder WithResolver(IWorkspaceResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    public MutationContextBuilder WithStageCodeActionAsync(Func<StageCodeActionRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> stageCodeActionAsync)
    {
        _stageCodeActionAsync = stageCodeActionAsync ?? throw new ArgumentNullException(nameof(stageCodeActionAsync));
        return this;
    }

    public MutationContextBuilder WithStageReplayCodeActionAsync(Func<ReplayCodeActionRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> stageReplayCodeActionAsync)
    {
        _stageReplayCodeActionAsync = stageReplayCodeActionAsync ?? throw new ArgumentNullException(nameof(stageReplayCodeActionAsync));
        return this;
    }

    public MutationContextBuilder WithStageCodeFixAsync(Func<StageCodeFixRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> stageCodeFixAsync)
    {
        _stageCodeFixAsync = stageCodeFixAsync ?? throw new ArgumentNullException(nameof(stageCodeFixAsync));
        return this;
    }

    public MutationContextBuilder WithStageFixAllAsync(Func<StageFixAllRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> stageFixAllAsync)
    {
        _stageFixAllAsync = stageFixAllAsync ?? throw new ArgumentNullException(nameof(stageFixAllAsync));
        return this;
    }

    public MutationContextBuilder WithStageScopedCodeFixAsync(Func<ScopedCodeFixRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> stageScopedCodeFixAsync)
    {
        _stageScopedCodeFixAsync = stageScopedCodeFixAsync ?? throw new ArgumentNullException(nameof(stageScopedCodeFixAsync));
        return this;
    }

    public MutationContextBuilder WithStageLocationCodeFixAsync(Func<LocationCodeFixRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> stageLocationCodeFixAsync)
    {
        _stageLocationCodeFixAsync = stageLocationCodeFixAsync ?? throw new ArgumentNullException(nameof(stageLocationCodeFixAsync));
        return this;
    }

    public MutationContextBuilder WithToolExecutionServices(IToolExecutionServices toolExecutionServices)
    {
        _toolExecutionServices = toolExecutionServices ?? throw new ArgumentNullException(nameof(toolExecutionServices));
        return this;
    }

    public MutationContextBuilder WithStageAsync(Func<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken, ValueTask<PluginExecutionResult<MutationData>>> stageAsync)
    {
        _stageAsync = stageAsync ?? throw new ArgumentNullException(nameof(stageAsync));
        return this;
    }

    public IMutationContext Build()
    {
        var resolver = _resolver ?? CreateDefaultResolver();
        var stageAsync = _stageAsync;
        var stageCodeActionAsync = _stageCodeActionAsync ?? CreateDefaultMutationProposalAsync<StageCodeActionRequest>();
        var stageReplayCodeActionAsync = _stageReplayCodeActionAsync ?? CreateDefaultMutationProposalAsync<ReplayCodeActionRequest>();
        var stageCodeFixAsync = _stageCodeFixAsync ?? CreateDefaultMutationProposalAsync<StageCodeFixRequest>();
        var stageFixAllAsync = _stageFixAllAsync ?? CreateDefaultMutationProposalAsync<StageFixAllRequest>();
        var stageScopedCodeFixAsync = _stageScopedCodeFixAsync ?? CreateDefaultMutationProposalAsync<ScopedCodeFixRequest>();
        var stageLocationCodeFixAsync = _stageLocationCodeFixAsync ?? CreateDefaultMutationProposalAsync<LocationCodeFixRequest>();
        if (stageAsync is null)
        {
            stageAsync = static (_, proposal, _, _, _) => ValueTask.FromResult(PluginExecutionResult<MutationData>.Success(new MutationData
            {
                Operation = "Operation",
                Summary = proposal.Summary,
            }));
        }

        var context = new Mock<ICodeActionMutationContext>();
        context.SetupGet(item => item.CurrentSolution).Returns(_currentSolution);
        context.SetupGet(item => item.WorkspaceIdentity).Returns(_workspaceIdentity);
        context.SetupGet(item => item.TransactionRevision).Returns(_transactionRevision);
        context.SetupGet(item => item.DefaultMaxResults).Returns(_defaultMaxResults);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver);
        context.SetupGet(item => item.ToolExecutionServices).Returns(_toolExecutionServices);
        context
            .Setup(item => item.StageAsync(
                It.IsAny<RegisteredTool>(),
                It.IsAny<MutationProposal>(),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                It.IsAny<CancellationToken>()))
            .Returns<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken>((tool, proposal, diagnostics, warnings, cancellationToken) =>
                stageAsync(tool, proposal, diagnostics, warnings, cancellationToken));
        context
            .Setup(item => item.StageCodeActionAsync(
                It.IsAny<StageCodeActionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<StageCodeActionRequest, CancellationToken>((request, cancellationToken) => stageCodeActionAsync(request, cancellationToken));
        context
            .Setup(item => item.StageReplayCodeActionAsync(
                It.IsAny<ReplayCodeActionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<ReplayCodeActionRequest, CancellationToken>((request, cancellationToken) => stageReplayCodeActionAsync(request, cancellationToken));
        context
            .Setup(item => item.StageCodeFixAsync(
                It.IsAny<StageCodeFixRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<StageCodeFixRequest, CancellationToken>((request, cancellationToken) => stageCodeFixAsync(request, cancellationToken));
        context
            .Setup(item => item.StageFixAllAsync(
                It.IsAny<StageFixAllRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<StageFixAllRequest, CancellationToken>((request, cancellationToken) => stageFixAllAsync(request, cancellationToken));
        context
            .Setup(item => item.StageScopedCodeFixAsync(
                It.IsAny<ScopedCodeFixRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<ScopedCodeFixRequest, CancellationToken>((request, cancellationToken) => stageScopedCodeFixAsync(request, cancellationToken));
        context
            .Setup(item => item.StageLocationCodeFixAsync(
                It.IsAny<LocationCodeFixRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<LocationCodeFixRequest, CancellationToken>((request, cancellationToken) => stageLocationCodeFixAsync(request, cancellationToken));
        return context.Object;
    }

    private static IWorkspaceResolver CreateDefaultResolver()
    {
        var resolver = new Mock<IWorkspaceResolver>();
        resolver.Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>())).Returns(SnapshotMatchResult.Matched());
        resolver.Setup(item => item.NormalizeDocumentPath(It.IsAny<string>())).Returns<string>(value => value.Replace('\\', '/'));
        resolver.Setup(item => item.NormalizeProjectPath(It.IsAny<string>())).Returns<string>(value => value.Replace('\\', '/'));
        return resolver.Object;
    }

    private static Func<TRequest, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> CreateDefaultMutationProposalAsync<TRequest>()
    {
        return static (_, _) => ValueTask.FromResult(PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "Code-action composition is unavailable.",
        }));
    }
}
