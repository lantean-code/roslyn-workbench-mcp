namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class WorkspacePathComparisonTests
{
    [Fact]
    public void GIVEN_CurrentOperatingSystem_WHEN_ReadingPolicy_THEN_ShouldExposePlatformDefaultComparison()
    {
        var target = new WorkspacePathComparison();
        var expectedComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var expectedComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        target.Comparison.Should().Be(expectedComparison);
        target.Comparer.Should().BeSameAs(expectedComparer);
    }
}
