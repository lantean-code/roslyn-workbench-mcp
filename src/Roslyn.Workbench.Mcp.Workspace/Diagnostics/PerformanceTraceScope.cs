using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.Workspace.Diagnostics;

internal readonly struct PerformanceTraceScope : IDisposable
{
    private readonly WorkbenchPerformanceEventSource? _eventSource;
    private readonly long _startedTimestamp;
    private readonly string? _operation;
    private readonly string? _phase;

    internal PerformanceTraceScope(
        WorkbenchPerformanceEventSource eventSource,
        string operation,
        string phase)
    {
        _eventSource = eventSource;
        _startedTimestamp = Stopwatch.GetTimestamp();
        _operation = operation;
        _phase = phase;
    }

    public void Dispose()
    {
        if (_eventSource is null || _operation is null || _phase is null)
        {
            return;
        }

        var elapsedMilliseconds = Stopwatch.GetElapsedTime(_startedTimestamp).TotalMilliseconds;
        _eventSource.PhaseCompleted(elapsedMilliseconds, _operation, _phase);
    }
}
