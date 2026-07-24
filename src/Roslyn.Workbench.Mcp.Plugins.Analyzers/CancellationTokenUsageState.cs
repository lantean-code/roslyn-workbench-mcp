namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal sealed class CancellationTokenUsageState
{
    private int _isObserved;

    public bool IsObserved => Volatile.Read(ref _isObserved) != 0;

    public void MarkObserved()
    {
        Interlocked.Exchange(ref _isObserved, 1);
    }
}
