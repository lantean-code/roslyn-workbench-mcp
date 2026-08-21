using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal readonly struct WorkspaceInputWatcherEvent
{
    public Exception? Error { get; }

    public WorkspaceInputChangeKind Kind { get; }

    [MemberNotNullWhen(true, nameof(Path))]
    public bool HasPath => Kind != WorkspaceInputChangeKind.WatcherError;

    [MemberNotNullWhen(true, nameof(PreviousPath))]
    public bool HasPreviousPath => PreviousPath is not null;

    public string? Path { get; }

    public string? PreviousPath { get; }

    private WorkspaceInputWatcherEvent(
        WorkspaceInputChangeKind kind,
        string? path,
        string? previousPath,
        Exception? error)
    {
        Kind = kind;
        Path = path;
        PreviousPath = previousPath;
        Error = error;
    }

    public static WorkspaceInputWatcherEvent Changed(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Changed,
            path,
            null,
            null);
    }

    public static WorkspaceInputWatcherEvent Created(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Created,
            path,
            null,
            null);
    }

    public static WorkspaceInputWatcherEvent Deleted(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Deleted,
            path,
            null,
            null);
    }

    public static WorkspaceInputWatcherEvent Renamed(string path, string previousPath)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Renamed,
            path,
            previousPath,
            null);
    }

    public static WorkspaceInputWatcherEvent WatcherError(Exception error)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.WatcherError,
            null,
            null,
            error);
    }
}
