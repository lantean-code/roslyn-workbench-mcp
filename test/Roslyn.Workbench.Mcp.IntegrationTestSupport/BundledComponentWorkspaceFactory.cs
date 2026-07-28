namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal static class BundledComponentWorkspaceFactory
{
    internal static ComponentWorkspace CreateInspectionWorkspace(int defaultMaxResults = 100)
    {
        return ComponentWorkspace.Create(new ComponentWorkspaceOptions
        {
            Boundary = ComponentWorkspaceBoundary.Plugins,
            DefaultMaxResults = defaultMaxResults,
        });
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
        return CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });
    }

    internal static ComponentWorkspace CreateTestCodeActionWorkspace(ICodeActionComposition composition)
    {
        return CreateCodeActionWorkspace(composition, ControlledCodeActionDescriptorClassifier.Classify);
    }

    private static ComponentWorkspace CreateCodeActionWorkspace(
        ICodeActionComposition composition,
        CodeActionDescriptorOverride? descriptorOverride = null)
    {
        return ComponentWorkspace.Create(
            new ComponentWorkspaceOptions
            {
                Boundary = ComponentWorkspaceBoundary.CodeActions,
            },
            composition,
            descriptorOverride);
    }

    internal static SnapshotPrecondition CreateSnapshot(
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        int? transactionRevision = null)
    {
        return new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.Context.WorkspaceEpoch!.Value,
            TransactionRevision = transactionRevision,
        };
    }
}
