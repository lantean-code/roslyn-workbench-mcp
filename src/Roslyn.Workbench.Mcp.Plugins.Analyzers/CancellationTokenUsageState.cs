namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Tracks whether concurrent operation analysis has observed meaningful use of a handler's cancellation token.
/// </summary>
internal sealed class CancellationTokenUsageState
{
    private int _isObserved;

    /// <summary>
    /// Gets a value indicating whether any registered operation has observed the cancellation token.
    /// </summary>
    public bool IsObserved => Volatile.Read(ref _isObserved) != 0;

    /// <summary>
    /// Records that cancellation-token use was observed in a thread-safe manner.
    /// </summary>
    public void MarkObserved()
    {
        Interlocked.Exchange(ref _isObserved, 1);
    }
}
