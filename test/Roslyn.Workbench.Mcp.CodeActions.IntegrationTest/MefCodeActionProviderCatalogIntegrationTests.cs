using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Host.Mef;
using Roslyn.Workbench.Mcp.CodeActions.Composition;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class MefCodeActionProviderCatalogIntegrationTests
{
    [Fact]
    public void GIVEN_RoslynMefHost_WHEN_ReadingExportsThroughCompatibilityAdapter_THEN_ShouldReturnTypedProviders()
    {
        var assemblies = MefHostServices.DefaultAssemblies
            .Append(typeof(TestRefactoringProvider).Assembly)
            .Distinct(CodeActionAssemblyIdentityComparer.Instance);
        var hostServices = MefHostServices.Create(assemblies);
        var target = new MefHostExportProviderCompatibilityAdapter();

        var result = target.ReadExports<CodeRefactoringProvider>(hostServices);

        result.IsSuccessful.Should().BeTrue();
        result.Exports.Should().Contain(provider => provider is TestRefactoringProvider);
    }

    [Fact]
    public void GIVEN_NoBuiltInAssembliesAndNoProviderAssemblies_WHEN_CreatingCatalog_THEN_ShouldReportUnavailableStatus()
    {
        var target = CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
        });

        target.Status.IsAvailable.Should().BeFalse();
        target.WorkspaceHostServices.Should().BeNull();
    }

    [Fact]
    public void GIVEN_TestProviderAssembly_WHEN_CreatingCatalog_THEN_ShouldReportAvailableStatus()
    {
        var target = CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });

        target.Status.IsAvailable.Should().BeTrue();
        target.WorkspaceHostServices.Should().NotBeNull();
    }
}
