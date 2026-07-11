namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceCommitEntry
{
    public required string TargetPath { get; init; }

    public required WorkspaceFileOperation Operation { get; init; }

    public required bool OriginalExists { get; init; }

    public string? OriginalHash { get; init; }

    public string? IntendedHash { get; init; }

    public string? BackupPath { get; init; }

    public string? StagedPath { get; init; }

    public string? DeleteMarkerPath { get; init; }

    public string GetRequiredBackupPath()
    {
        return BackupPath ?? throw new InvalidOperationException("The commit entry does not contain a backup path.");
    }

    public string GetRequiredStagedPath()
    {
        return StagedPath ?? throw new InvalidOperationException("The commit entry does not contain a staged path.");
    }

    public string GetRequiredDeleteMarkerPath()
    {
        return DeleteMarkerPath ?? throw new InvalidOperationException("The commit entry does not contain a delete marker path.");
    }
}
