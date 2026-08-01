namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal static class BundledComponentWorkspaceFactory
{
    internal static ComponentWorkspace CreateInspectionWorkspace(int defaultMaxResults = 100)
    {
        var options = new ComponentWorkspaceOptions
        {
            Boundary = ComponentWorkspaceBoundary.Plugins,
            DefaultMaxResults = defaultMaxResults,
        };

        return ComponentWorkspace.Create(options);
    }

    internal static ComponentWorkspace CreateBuiltInCodeActionWorkspace()
    {
        var composition = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        return CreateCodeActionWorkspace(composition);
    }

    internal static ICodeActionComposition CreateTestCodeActionComposition()
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

    internal static ComponentWorkspace CreateTestCodeActionWorkspace(ICodeActionComposition composition)
    {
        return CreateCodeActionWorkspace(composition);
    }

    private static ComponentWorkspace CreateCodeActionWorkspace(ICodeActionComposition composition)
    {
        var options = new ComponentWorkspaceOptions
        {
            Boundary = ComponentWorkspaceBoundary.CodeActions,
        };

        return ComponentWorkspace.Create(options, composition);
    }

    internal static SnapshotPrecondition CreateSnapshot(
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        int? transactionRevision = null)
    {
        var workspaceId = openResult.Context.WorkspaceId
            ?? throw new InvalidOperationException("The opened workspace did not return a workspace identifier.");

        return new SnapshotPrecondition
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = openResult.Context.WorkspaceEpoch!.Value,
            TransactionRevision = transactionRevision,
        };
    }
}
