using AwesomeAssertions;

using Roslyn.Workbench.Mcp.TestSupport;

using Xunit;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class CodeActionRuntimeFactoryTests
{
    [Fact]
    public void GIVEN_NoBuiltInAssembliesAndNoProviderAssemblies_WHEN_CreatingRuntime_THEN_ShouldReportUnavailableStatus()
    {
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = false,
        });

        runtime.CodeActionService.Status.IsAvailable.Should().BeFalse();
        runtime.WorkspaceHostServices.Should().BeNull();
    }

    [Fact]
    public void GIVEN_TestProviderAssembly_WHEN_CreatingRuntime_THEN_ShouldReportAvailableStatus()
    {
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });

        runtime.CodeActionService.Status.IsAvailable.Should().BeTrue();
        runtime.WorkspaceHostServices.Should().NotBeNull();
    }
}
