using Roslyn.Workbench.Mcp.CodeActions.Composition;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class MefCodeActionCompositionIntegrationTests
{
    [Fact]
    public void GIVEN_TestProviderAssembly_WHEN_ComposingCatalog_THEN_ShouldReturnTypedProvidersAndHostServices()
    {
        var target = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });

        target.Status.IsAvailable.Should().BeTrue();
        target.WorkspaceHostServices.Should().NotBeNull();
        target.RefactoringProviders.Should().ContainSingle(provider => provider is TestRefactoringProvider);
        target.CodeFixProviders.Should().ContainSingle(provider => provider is TestCodeFixProvider);
    }
}
