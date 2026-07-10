using Roslyn.Workbench.Mcp.CodeActions.Composition;
namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class CodeActionRuntimeComposerIntegrationTests
{
    [Fact]
    public void GIVEN_NoBuiltInAssembliesAndNoProviderAssemblies_WHEN_ComposingRuntime_THEN_ShouldReportUnavailableStatus()
    {
        var target = new CodeActionRuntimeComposer();

        var result = target.Compose(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = false,
        });

        result.Status.IsAvailable.Should().BeFalse();
        result.WorkspaceHostServices.Should().BeNull();
    }

    [Fact]
    public void GIVEN_TestProviderAssembly_WHEN_ComposingRuntime_THEN_ShouldReportAvailableStatus()
    {
        var target = new CodeActionRuntimeComposer();

        var result = target.Compose(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });

        result.Status.IsAvailable.Should().BeTrue();
        result.WorkspaceHostServices.Should().NotBeNull();
    }
}
