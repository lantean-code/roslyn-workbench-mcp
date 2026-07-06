using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class QueryContextBuilder
{
    private Solution _currentSolution = new AdhocWorkspace().CurrentSolution;
    private WorkspaceIdentity _workspaceIdentity = default!;
    private int? _transactionRevision;
    private ResultLimit _effectiveResultLimit = new();
    private int _maxResponseBytes = 1024 * 1024;
    private IWorkspaceResolver? _resolver;
    private ICodeActionService? _codeActionService;
    private IToolExecutionServices _toolExecutionServices = new ToolExecutionServicesBuilder().Build();

    public QueryContextBuilder WithCurrentSolution(Solution currentSolution)
    {
        _currentSolution = currentSolution ?? throw new ArgumentNullException(nameof(currentSolution));
        return this;
    }

    public QueryContextBuilder WithWorkspaceIdentity(WorkspaceIdentity workspaceIdentity)
    {
        _workspaceIdentity = workspaceIdentity;
        return this;
    }

    public QueryContextBuilder WithTransactionRevision(int? transactionRevision)
    {
        _transactionRevision = transactionRevision;
        return this;
    }

    public QueryContextBuilder WithEffectiveResultLimit(ResultLimit effectiveResultLimit)
    {
        _effectiveResultLimit = effectiveResultLimit ?? throw new ArgumentNullException(nameof(effectiveResultLimit));
        return this;
    }

    public QueryContextBuilder WithMaxResponseBytes(int maxResponseBytes)
    {
        _maxResponseBytes = maxResponseBytes;
        return this;
    }

    public QueryContextBuilder WithResolver(IWorkspaceResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    public QueryContextBuilder WithCodeActionService(ICodeActionService codeActionService)
    {
        _codeActionService = codeActionService ?? throw new ArgumentNullException(nameof(codeActionService));
        return this;
    }

    public QueryContextBuilder WithToolExecutionServices(IToolExecutionServices toolExecutionServices)
    {
        _toolExecutionServices = toolExecutionServices ?? throw new ArgumentNullException(nameof(toolExecutionServices));
        return this;
    }

    public IQueryContext Build()
    {
        var resolver = _resolver ?? CreateDefaultResolver();
        var codeActionService = _codeActionService ?? CreateDefaultCodeActionService();
        var context = new Mock<IQueryContext>();
        context.SetupGet(item => item.CurrentSolution).Returns(_currentSolution);
        context.SetupGet(item => item.WorkspaceIdentity).Returns(_workspaceIdentity);
        context.SetupGet(item => item.TransactionRevision).Returns(_transactionRevision);
        context.SetupGet(item => item.EffectiveResultLimit).Returns(_effectiveResultLimit);
        context.SetupGet(item => item.MaxResponseBytes).Returns(_maxResponseBytes);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver);
        context.SetupGet(item => item.CodeActionService).Returns(codeActionService);
        context.SetupGet(item => item.ToolExecutionServices).Returns(_toolExecutionServices);
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
