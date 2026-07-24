namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal readonly struct WorkspaceInputWatcherEvent
{
    public Exception? Error { get; }

    public WorkspaceInputWatcherEventKind Kind { get; }

    public string? Path { get; }

    public string? PreviousPath { get; }

    private WorkspaceInputWatcherEvent(
        WorkspaceInputWatcherEventKind kind,
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
            WorkspaceInputWatcherEventKind.Changed,
            path,
            null,
            null);
    }

    public static WorkspaceInputWatcherEvent Created(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputWatcherEventKind.Created,
            path,
            null,
            null);
    }

    public static WorkspaceInputWatcherEvent Deleted(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputWatcherEventKind.Deleted,
            path,
            null,
            null);
    }

    public static WorkspaceInputWatcherEvent Renamed(string path, string previousPath)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputWatcherEventKind.Renamed,
            path,
            previousPath,
            null);
    }

    public static WorkspaceInputWatcherEvent WatcherError(Exception error)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputWatcherEventKind.Error,
            null,
            null,
            error);
    }
}
