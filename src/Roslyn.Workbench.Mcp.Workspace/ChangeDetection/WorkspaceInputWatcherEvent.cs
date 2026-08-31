using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Normalizes filesystem watcher callbacks into a single immutable event shape.
/// </summary>
internal readonly struct WorkspaceInputWatcherEvent
{
    /// <summary>
    /// Gets the watcher exception when <see cref="Kind"/> is <see cref="WorkspaceInputChangeKind.WatcherError"/>.
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Gets the normalized event category.
    /// </summary>
    public WorkspaceInputChangeKind Kind { get; }

    /// <summary>
    /// Gets whether this event carries an affected path.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Path))]
    public bool HasPath => Kind != WorkspaceInputChangeKind.WatcherError;

    /// <summary>
    /// Gets whether this event carries a former path for a rename.
    /// </summary>
    [MemberNotNullWhen(true, nameof(PreviousPath))]
    public bool HasPreviousPath => PreviousPath is not null;

    /// <summary>
    /// Gets the affected path for a filesystem change.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets the former path for a rename event.
    /// </summary>
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

    /// <summary>
    /// Creates an event for a modified file or directory.
    /// </summary>
    /// <param name="path">The changed path.</param>
    /// <returns>The normalized watcher event.</returns>
    public static WorkspaceInputWatcherEvent Changed(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Changed,
            path,
            null,
            null);
    }

    /// <summary>
    /// Creates an event for a newly created file or directory.
    /// </summary>
    /// <param name="path">The created path.</param>
    /// <returns>The normalized watcher event.</returns>
    public static WorkspaceInputWatcherEvent Created(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Created,
            path,
            null,
            null);
    }

    /// <summary>
    /// Creates an event for a deleted file or directory.
    /// </summary>
    /// <param name="path">The deleted path.</param>
    /// <returns>The normalized watcher event.</returns>
    public static WorkspaceInputWatcherEvent Deleted(string path)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Deleted,
            path,
            null,
            null);
    }

    /// <summary>
    /// Creates an event for a renamed or moved file or directory.
    /// </summary>
    /// <param name="path">The new path.</param>
    /// <param name="previousPath">The former path.</param>
    /// <returns>The normalized watcher event.</returns>
    public static WorkspaceInputWatcherEvent Renamed(string path, string previousPath)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.Renamed,
            path,
            previousPath,
            null);
    }

    /// <summary>
    /// Creates an event for a watcher failure that makes subsequent coverage unreliable.
    /// </summary>
    /// <param name="error">The watcher exception.</param>
    /// <returns>The normalized watcher error event.</returns>
    public static WorkspaceInputWatcherEvent WatcherError(Exception error)
    {
        return new WorkspaceInputWatcherEvent(
            WorkspaceInputChangeKind.WatcherError,
            null,
            null,
            error);
    }
}
