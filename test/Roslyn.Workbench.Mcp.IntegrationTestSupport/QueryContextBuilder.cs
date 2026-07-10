using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class QueryContextBuilder
{
    private Solution _currentSolution = new AdhocWorkspace().CurrentSolution;
    private WorkspaceIdentity _workspaceIdentity = default!;
    private int? _transactionRevision;
    private int _defaultMaxResults = 100;
    private IWorkspaceResolver? _resolver;
    private IToolExecutionServices _toolExecutionServices = new ToolExecutionServicesBuilder().Build();
    private Func<ListCodeActionsRequest, CancellationToken, ValueTask<PluginExecutionResult<CodeActionListData>>>? _listCodeActionsAsync;
    private Func<DescribeCodeActionRequest, CancellationToken, ValueTask<PluginExecutionResult<DescribeCodeActionData>>>? _describeCodeActionAsync;

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

    public QueryContextBuilder WithDefaultMaxResults(int defaultMaxResults)
    {
        _defaultMaxResults = defaultMaxResults;
        return this;
    }

    public QueryContextBuilder WithResolver(IWorkspaceResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    public QueryContextBuilder WithListCodeActionsAsync(Func<ListCodeActionsRequest, CancellationToken, ValueTask<PluginExecutionResult<CodeActionListData>>> listCodeActionsAsync)
    {
        _listCodeActionsAsync = listCodeActionsAsync ?? throw new ArgumentNullException(nameof(listCodeActionsAsync));
        return this;
    }

    public QueryContextBuilder WithDescribeCodeActionAsync(Func<DescribeCodeActionRequest, CancellationToken, ValueTask<PluginExecutionResult<DescribeCodeActionData>>> describeCodeActionAsync)
    {
        _describeCodeActionAsync = describeCodeActionAsync ?? throw new ArgumentNullException(nameof(describeCodeActionAsync));
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
        var listCodeActionsAsync = _listCodeActionsAsync ?? CreateDefaultListCodeActionsAsync();
        var describeCodeActionAsync = _describeCodeActionAsync ?? CreateDefaultDescribeCodeActionAsync();
        var context = new Mock<ICodeActionQueryContext>();
        context.SetupGet(item => item.CurrentSolution).Returns(_currentSolution);
        context.SetupGet(item => item.WorkspaceIdentity).Returns(_workspaceIdentity);
        context.SetupGet(item => item.TransactionRevision).Returns(_transactionRevision);
        context.SetupGet(item => item.DefaultMaxResults).Returns(_defaultMaxResults);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver);
        context.SetupGet(item => item.ToolExecutionServices).Returns(_toolExecutionServices);
        context
            .Setup(item => item.ListCodeActionsAsync(
                It.IsAny<ListCodeActionsRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<ListCodeActionsRequest, CancellationToken>((request, cancellationToken) => listCodeActionsAsync(request, cancellationToken));
        context
            .Setup(item => item.DescribeCodeActionAsync(
                It.IsAny<DescribeCodeActionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<DescribeCodeActionRequest, CancellationToken>((request, cancellationToken) => describeCodeActionAsync(request, cancellationToken));
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

    private static Func<ListCodeActionsRequest, CancellationToken, ValueTask<PluginExecutionResult<CodeActionListData>>> CreateDefaultListCodeActionsAsync()
    {
        return static (_, _) => ValueTask.FromResult(PluginExecutionResult<CodeActionListData>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "Code-action composition is unavailable.",
        }));
    }

    private static Func<DescribeCodeActionRequest, CancellationToken, ValueTask<PluginExecutionResult<DescribeCodeActionData>>> CreateDefaultDescribeCodeActionAsync()
    {
        return static (_, _) => ValueTask.FromResult(PluginExecutionResult<DescribeCodeActionData>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "Code-action composition is unavailable.",
        }));
    }
}
