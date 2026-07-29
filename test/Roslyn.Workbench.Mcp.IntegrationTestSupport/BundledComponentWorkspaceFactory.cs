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
        return CreateCodeActionWorkspace(composition, ControlledCodeActionDescriptorClassifier.Classify);
    }

    private static ComponentWorkspace CreateCodeActionWorkspace(
        ICodeActionComposition composition,
        CodeActionDescriptorOverride? descriptorOverride = null)
    {
        var options = new ComponentWorkspaceOptions
        {
            Boundary = ComponentWorkspaceBoundary.CodeActions,
        };

        return ComponentWorkspace.Create(
            options,
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
