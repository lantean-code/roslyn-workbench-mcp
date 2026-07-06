using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class MutationContextBuilder
{
    private Solution _currentSolution = new AdhocWorkspace().CurrentSolution;
    private WorkspaceIdentity _workspaceIdentity = default!;
    private int? _transactionRevision;
    private ResultLimit _effectiveResultLimit = new();
    private IWorkspaceResolver? _resolver;
    private ICodeActionService? _codeActionService;
    private IToolExecutionServices _toolExecutionServices = new ToolExecutionServicesBuilder().Build();
    private Func<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken, ValueTask<PluginExecutionResult<MutationData>>>? _stageAsync;

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

    public MutationContextBuilder WithEffectiveResultLimit(ResultLimit effectiveResultLimit)
    {
        _effectiveResultLimit = effectiveResultLimit ?? throw new ArgumentNullException(nameof(effectiveResultLimit));
        return this;
    }

    public MutationContextBuilder WithResolver(IWorkspaceResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    public MutationContextBuilder WithCodeActionService(ICodeActionService codeActionService)
    {
        _codeActionService = codeActionService ?? throw new ArgumentNullException(nameof(codeActionService));
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
        var codeActionService = _codeActionService ?? CreateDefaultCodeActionService();
        var stageAsync = _stageAsync;
        if (stageAsync is null)
        {
            stageAsync = static (_, proposal, _, _, _) => ValueTask.FromResult(PluginExecutionResult<MutationData>.Success(new MutationData
            {
                Operation = "Operation",
                Summary = proposal.Summary,
            }));
        }

        var context = new Mock<IMutationContext>();
        context.SetupGet(item => item.CurrentSolution).Returns(_currentSolution);
        context.SetupGet(item => item.WorkspaceIdentity).Returns(_workspaceIdentity);
        context.SetupGet(item => item.TransactionRevision).Returns(_transactionRevision);
        context.SetupGet(item => item.EffectiveResultLimit).Returns(_effectiveResultLimit);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver);
        context.SetupGet(item => item.CodeActionService).Returns(codeActionService);
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

    private static ICodeActionService CreateDefaultCodeActionService()
    {
        var service = new Mock<ICodeActionService>();
        service.SetupGet(item => item.Status).Returns(new ComponentStatus
        {
            IsAvailable = true,
            Version = "Version",
        });
        return service.Object;
    }
}
