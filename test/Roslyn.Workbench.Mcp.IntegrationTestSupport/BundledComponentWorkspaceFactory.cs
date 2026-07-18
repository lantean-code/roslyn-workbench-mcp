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
        var providerCatalog = CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        return CreateCodeActionWorkspace(providerCatalog);
    }

    internal static ICodeActionProviderCatalog CreateTestCodeActionProviderCatalog()
    {
        return CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });
    }

    internal static ComponentWorkspace CreateTestCodeActionWorkspace(ICodeActionProviderCatalog providerCatalog)
    {
        return CreateCodeActionWorkspace(providerCatalog, ControlledCodeActionDescriptorClassifier.Classify);
    }

    private static ComponentWorkspace CreateCodeActionWorkspace(
        ICodeActionProviderCatalog providerCatalog,
        CodeActionDescriptorOverride? descriptorOverride = null)
    {
        return ComponentWorkspace.Create(
            new ComponentWorkspaceOptions
            {
                Boundary = ComponentWorkspaceBoundary.CodeActions,
            },
            providerCatalog,
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
