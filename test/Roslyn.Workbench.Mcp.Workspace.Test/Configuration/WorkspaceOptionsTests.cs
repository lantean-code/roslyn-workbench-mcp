using Roslyn.Workbench.Mcp.Workspace.Configuration;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Configuration;

public sealed class WorkspaceOptionsTests
{
    [Fact]
    public void GIVEN_DefaultOptions_WHEN_ReadingValues_THEN_ShouldExposeOperationalDefaults()
    {
        var target = new WorkspaceOptions();

        target.MaxConcurrentQueries.Should().Be(2);
        target.DefaultMaxResults.Should().Be(100);
        target.MaxTransactionRevisions.Should().Be(20);
        target.MaxLoadedWorkspaces.Should().Be(4);
        target.StateDirectory.Should().Be(StateDirectoryDefaults.GetDefaultPath());
    }
}
