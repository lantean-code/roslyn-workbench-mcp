using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.Workspace.Diagnostics;

/// <summary>
/// Measures one enabled performance phase and emits its duration when disposed.
/// </summary>
internal readonly struct PerformanceTraceScope : IDisposable
{
    private readonly WorkbenchPerformanceEventSource? _eventSource;
    private readonly long _startedTimestamp;
    private readonly string? _operation;
    private readonly string? _phase;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceTraceScope"/> structure.
    /// </summary>
    /// <param name="eventSource">The event source that receives the completed duration.</param>
    /// <param name="operation">The containing operation.</param>
    /// <param name="phase">The measured phase.</param>
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

    /// <summary>
    /// Emits the elapsed duration when this scope represents an enabled trace.
    /// </summary>
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
