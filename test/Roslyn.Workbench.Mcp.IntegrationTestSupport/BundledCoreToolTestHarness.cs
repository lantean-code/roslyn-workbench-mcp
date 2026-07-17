using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class BundledCoreToolTestHarness
{
    public static IWorkspaceRuntime CreateInspectionCoordinator(int defaultMaxResults = 100)
    {
        return WorkspaceCoordinatorFactory.Create(new WorkspaceRuntimeOptions
        {
            DefaultMaxResults = defaultMaxResults,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
    }

    public static IWorkspaceRuntime CreateBuiltInCodeActionCoordinator()
    {
        var providerCatalog = CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        return WorkspaceCoordinatorFactory.CreateWithCodeActionProviderCatalog(providerCatalog, BundledCoreToolExecutionServicesFactory.Create());
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

    internal static IWorkspaceRuntime CreateTestCodeActionCoordinator(ICodeActionProviderCatalog providerCatalog)
    {
        return WorkspaceCoordinatorFactory.CreateWithCodeActionProviderCatalog(
            providerCatalog,
            BundledCoreToolExecutionServicesFactory.Create());
    }

    public static SnapshotPrecondition CreateSnapshot(ToolResult<WorkspaceOpenData> openResult, int? transactionRevision = null)
    {
        return new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            TransactionRevision = transactionRevision,
        };
    }

}
