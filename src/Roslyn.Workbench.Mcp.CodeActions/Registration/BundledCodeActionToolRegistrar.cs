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
                "Lists bounded Roslyn code fixes and refactorings for a document, selection or caret."));

        registry.RegisterQueryTool<PrepareFixAllTool, PrepareFixAllRequest, PrepareFixAllData>(
            CreateReferenceProducingQueryMetadata(
                _prepareFixAllName,
                "Prepare Fix All",
                "Revalidates a Code Fix and reports the bounded impact of one explicit Fix All scope without staging changes."));

        if (includeMutationTools)
        {
            registry.RegisterMutationTool<StageCodeActionTool, StageCodeActionRequest>(
                CreateMutationMetadata(
                    _stageCodeActionName,
                    "Stage Code Action",
                    "Revalidates and stages one selected Code Fix, refactoring or prepared Fix All action into the active transaction."));
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
