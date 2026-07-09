using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class CodeActionRuntimeComposerTests
{
    [Fact]
    public void GIVEN_NoBuiltInAssembliesAndNoProviderAssemblies_WHEN_ComposingRuntime_THEN_ShouldReportUnavailableStatus()
    {
        var runtime = Compose(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = false,
        });

        runtime.Status.IsAvailable.Should().BeFalse();
        runtime.WorkspaceHostServices.Should().BeNull();
    }

    [Fact]
    public void GIVEN_TestProviderAssembly_WHEN_ComposingRuntime_THEN_ShouldReportAvailableStatus()
    {
        var runtime = Compose(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });

        runtime.Status.IsAvailable.Should().BeTrue();
        runtime.WorkspaceHostServices.Should().NotBeNull();
    }

    private static CodeActionRuntime Compose(CodeActionRuntimeOptions options)
    {
        return new CodeActionRuntimeComposer().Compose(options);
    }
}
