using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.CodeActions.Composition;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class MefCodeActionProviderCatalogIntegrationTests
{
    [Fact]
    public void GIVEN_NoBuiltInAssembliesAndNoProviderAssemblies_WHEN_CreatingCatalog_THEN_ShouldReportUnavailableStatus()
    {
        var target = new MefCodeActionProviderCatalog(Options.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
        }));

        target.Status.IsAvailable.Should().BeFalse();
        target.WorkspaceHostServices.Should().BeNull();
    }

    [Fact]
    public void GIVEN_TestProviderAssembly_WHEN_CreatingCatalog_THEN_ShouldReportAvailableStatus()
    {
        var target = new MefCodeActionProviderCatalog(Options.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        }));

        target.Status.IsAvailable.Should().BeTrue();
        target.WorkspaceHostServices.Should().NotBeNull();
    }
}
