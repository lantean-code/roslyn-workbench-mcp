namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal static class BundledComponentWorkspaceFactory
{
    public static ComponentWorkspace CreateInspectionWorkspace(int defaultMaxResults = 100)
    {
        var options = new ComponentWorkspaceOptions
        {
            Boundary = ComponentWorkspaceBoundary.Plugins,
            DefaultMaxResults = defaultMaxResults,
        };

        return ComponentWorkspace.Create(options);
    }

    public static ComponentWorkspace CreateBuiltInCodeActionWorkspace()
    {
        var composition = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        return CreateCodeActionWorkspace(composition, includeBuiltInCodeActions: true);
    }

    public static ICodeActionComposition CreateTestCodeActionComposition()
    {
        var options = new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        };

        return CodeActionCompositionFactory.Create(options);
    }

    public static ComponentWorkspace CreateTestCodeActionWorkspace(ICodeActionComposition composition)
    {
        return CreateCodeActionWorkspace(composition, includeBuiltInCodeActions: false);
    }

    private static ComponentWorkspace CreateCodeActionWorkspace(ICodeActionComposition composition, bool includeBuiltInCodeActions)
    {
        var options = new ComponentWorkspaceOptions
        {
            Boundary = ComponentWorkspaceBoundary.CodeActions,
            IncludeBuiltInCodeActions = includeBuiltInCodeActions,
        };

        return ComponentWorkspace.Create(options, composition);
    }

    public static SnapshotPrecondition CreateSnapshot<TOutcome>(
        WorkspaceOperationResult<TOutcome> result)
    {
        return result.Context.Snapshot
            ?? throw new InvalidOperationException("The workspace operation did not return a snapshot.");
    }

    public static SnapshotPrecondition CreateTransactionStartSnapshot<TOutcome>(
        WorkspaceOperationResult<TOutcome> result)
    {
        var snapshot = CreateSnapshot(result);
        if (snapshot.TransactionRevision is not null)
        {
            throw new InvalidOperationException("The workspace operation snapshot is already within a transaction.");
        }

        return snapshot with { TransactionRevision = 0 };
    }

    public static SnapshotPrecondition CreateSnapshot(PluginExecutionResult<MutationData> result)
    {
        return result.Data?.Snapshot
            ?? throw new InvalidOperationException("The plugin mutation did not return a snapshot.");
    }

    public static SnapshotPrecondition CreateSnapshot(CodeActionExecutionResult<MutationData> result)
    {
        return result.Data?.Snapshot
            ?? throw new InvalidOperationException("The code action mutation did not return a snapshot.");
    }
}
