namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class FixAllActionFactory : IFixAllActionFactory
{
    private readonly ICodeActionDiagnosticService _diagnosticService;

    public FixAllActionFactory(ICodeActionDiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;
    }

    public Task<FixAllActionCreationResult> CreateAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        TextSpan originSpan,
        FixAllScope scope,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            document,
            scope is FixAllScope.ContainingMember or FixAllScope.ContainingType ? originSpan : null,
            provider,
            scope,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds, syntheticDiagnosticId),
            cancellationToken);

        return CreateCoreAsync(fixAllProvider, fixAllContext);
    }

    public Task<FixAllActionCreationResult> CreateAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            project,
            provider,
            FixAllScope.Project,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds, syntheticDiagnosticId),
            cancellationToken);

        return CreateCoreAsync(fixAllProvider, fixAllContext);
    }

    private static async Task<FixAllActionCreationResult> CreateCoreAsync(
        FixAllProvider fixAllProvider,
        FixAllContext fixAllContext)
    {
        var action = await fixAllProvider.GetFixAsync(fixAllContext);
        if (action is not null)
        {
            return FixAllActionCreationResult.Created(action);
        }

        var failure = new FixAllActionCreationFailure
        {
            Message = "The selected code fix could not produce a fix-all action.",
        };

        return FixAllActionCreationResult.Failed(failure);
    }
}
