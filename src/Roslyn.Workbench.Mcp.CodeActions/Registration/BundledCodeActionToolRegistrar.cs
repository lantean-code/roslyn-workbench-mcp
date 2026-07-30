namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal static class BundledCodeActionToolRegistrar
{
    public static void RegisterAll(ICodeActionToolRegistry registry)
    {
        registry.RegisterQueryTool<ListCodeActionsTool, ListCodeActionsRequest, CodeActionListData>(
            CreateMetadata(
                "list-code-actions",
                "List Code Actions",
                "Lists bounded Roslyn code fixes and refactorings for a document, selection or caret."));

        registry.RegisterQueryTool<PrepareFixAllTool, PrepareFixAllRequest, PrepareFixAllData>(
            CreateMetadata(
                "prepare-fix-all",
                "Prepare Fix All",
                "Revalidates a Code Fix and reports the bounded impact of one explicit Fix All scope without staging changes."));

        registry.RegisterMutationTool<StageCodeActionTool, StageCodeActionRequest>(
            CreateMutationMetadata(
                "stage-code-action",
                "Stage Code Action",
                "Revalidates and stages one selected Code Fix, refactoring or prepared Fix All action into the active transaction."));
    }

    private static CodeActionToolMetadata CreateMetadata(string name, string title, string description)
    {
        return new CodeActionToolMetadata
        {
            Name = name,
            Title = title,
            Description = description,
        };
    }

    private static CodeActionToolMetadata CreateMutationMetadata(string name, string title, string description)
    {
        var behavior = new CodeActionToolBehavior
        {
            Destructive = true,
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
