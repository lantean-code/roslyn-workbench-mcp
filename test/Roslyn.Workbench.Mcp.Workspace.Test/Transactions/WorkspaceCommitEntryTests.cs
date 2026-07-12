namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceCommitEntryTests
{
    [Fact]
    public void GIVEN_BackupPathIsMissing_WHEN_GettingRequiredBackupPath_THEN_ShouldThrowInvalidOperationException()
    {
        var target = CreateEntry();

        var action = target.GetRequiredBackupPath;

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_StagedPathIsMissing_WHEN_GettingRequiredStagedPath_THEN_ShouldThrowInvalidOperationException()
    {
        var target = CreateEntry();

        var action = target.GetRequiredStagedPath;

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_DeleteMarkerPathIsMissing_WHEN_GettingRequiredDeleteMarkerPath_THEN_ShouldThrowInvalidOperationException()
    {
        var target = CreateEntry();

        var action = target.GetRequiredDeleteMarkerPath;

        action.Should().Throw<InvalidOperationException>();
    }

    private static WorkspaceCommitEntry CreateEntry()
    {
        return new WorkspaceCommitEntry
        {
            TargetPath = "TargetPath",
            Operation = WorkspaceFileOperation.Replace,
            OriginalExists = true,
        };
    }
}
