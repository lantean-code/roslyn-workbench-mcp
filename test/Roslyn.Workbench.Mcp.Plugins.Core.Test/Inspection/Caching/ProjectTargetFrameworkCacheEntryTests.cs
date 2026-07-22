using Roslyn.Workbench.Mcp.Plugins.Core.Inspection.Caching;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection.Caching;

public sealed class ProjectTargetFrameworkCacheEntryTests
{
    [Fact]
    public void GIVEN_TargetFrameworks_WHEN_CreatingEntry_THEN_ShouldCopyValuesAndCalculateSize()
    {
        var targetFrameworks = new List<string> { "net10.0" };
        var target = new ProjectTargetFrameworkCacheEntry(ProjectTargetFrameworksResult.Succeeded(targetFrameworks));

        targetFrameworks.Add("net9.0");

        target.Result.TargetFrameworks.Should().Equal("net10.0");
        target.Size.Should().Be(2);
    }
}
