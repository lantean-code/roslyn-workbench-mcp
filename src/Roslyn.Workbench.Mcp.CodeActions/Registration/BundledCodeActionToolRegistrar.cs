namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Defines and registers the Code Action tools published by the host.
/// </summary>
internal static class BundledCodeActionToolRegistrar
{
    private const string _listCodeActionsName = "list-code-actions";
    private const string _prepareFixAllName = "prepare-fix-all";
    private const string _stageCodeActionName = "stage-code-action";

    /// <summary>
    /// Gets every host-owned Code Action tool name regardless of the active publication policy.
    /// </summary>
    public static IReadOnlyList<string> ToolNames { get; } = Array.AsReadOnly(
    [
        _listCodeActionsName,
        _prepareFixAllName,
        _stageCodeActionName,
    ]);

    /// <summary>
    /// Registers each permitted bundled Code Action tool with the supplied registry.
    /// </summary>
    /// <param name="registry">The registry populated with bundled Code Action tools.</param>
    /// <param name="includeMutationTools">Whether mutation tools are created and registered.</param>
    public static void RegisterAll(
        ICodeActionToolRegistry registry,
        bool includeMutationTools)
    {
        registry.RegisterQueryTool<ListCodeActionsTool, ListCodeActionsRequest, CodeActionListData>(
            CreateReferenceProducingQueryMetadata(
                _listCodeActionsName,
                "List Code Actions",
                "Discovers applicable Roslyn Code Fixes and refactorings for a document, selection or caret. Use this to choose an actionId for stage-code-action in mutation-enabled modes, or a Code Fix with advertised fixAllScopes for prepare-fix-all. Echo the current expectedSnapshot. Omitted range selects the whole document; a zero-length range selects a caret, using zero-based UTF-16 positions. Returns up to 50 action leaves by default; check actions.hasMore. Action IDs are temporary, process-local and tied to the exact workspace snapshot: rediscover after changes or expiry. Listing stores references but does not stage changes or write source files."));

        registry.RegisterQueryTool<PrepareFixAllTool, PrepareFixAllRequest, PrepareFixAllData>(
            CreateReferenceProducingQueryMetadata(
                _prepareFixAllName,
                "Prepare Fix All",
                "Evaluates one supported Document, Project or Solution Fix All operation for a Code Fix from list-code-actions, without staging or writing source files. Supply the originating actionId, an advertised fixAllScopes value and the matching expectedSnapshot; refactorings and already-prepared Fix All references are not accepted. Rejects operations exceeding maxChanges source documents, default 50. The affectedDocuments preview is limited separately to 20 identities by default; check its hasMore and totalCount. Returns a new temporary actionId for stage-code-action in mutation-enabled modes: use this prepared ID, not the originating ID, with the same snapshot. Staging revalidates the exact prepared change set."));

        if (includeMutationTools)
        {
            registry.RegisterMutationTool<StageCodeActionTool, StageCodeActionRequest>(
                CreateMutationMetadata(
                    _stageCodeActionName,
                    "Stage Code Action",
                    "Revalidates and stages one selected Code Fix, refactoring or prepared Fix All operation in the active transaction without writing source files. Start the transaction before discovery or preparation, then supply the actionId from list-code-actions or the new prepared ID from prepare-fix-all with its matching expectedSnapshot. Prepared Fix All staging must match the exact prepared change set and document limit. Successful staging consumes the reference; use the returned snapshot and rediscover before selecting another action. Inspect transaction-preview, or transaction-review in approval-required mode, then transaction-commit to write files or transaction-rollback to discard staged work."));
        }
    }

    private static CodeActionToolMetadata CreateReferenceProducingQueryMetadata(
        string name,
        string title,
        string description)
    {
        var behavior = new CodeActionToolBehavior
        {
            Idempotent = false,
        };

        return new CodeActionToolMetadata
        {
            Name = name,
            Title = title,
            Description = description,
            Behavior = behavior,
        };
    }

    private static CodeActionToolMetadata CreateMutationMetadata(string name, string title, string description)
    {
        var behavior = new CodeActionToolBehavior
        {
            Destructive = true,
            Idempotent = false,
        };

        return new CodeActionToolMetadata
        {
            Name = name,
            Title = title,
            Description = description,
            Behavior = behavior,
        };
    }
}
