using System.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Diagnostics;

internal sealed class WorkbenchPerformanceEventListener : EventListener
{
    private readonly List<EventWrittenEventArgs> _events = [];
    private readonly Lock _syncRoot = new();
    private TaskCompletionSource _eventWritten = CreateEventSource();

    public IReadOnlyList<EventWrittenEventArgs> Events
    {
        get
        {
            lock (_syncRoot)
            {
                return [.. _events];
            }
        }
    }

    public WorkbenchPerformanceEventListener(EventSource eventSource)
    {
        EnableEvents(eventSource, EventLevel.Informational);
    }

    public async Task WaitForEventAsync(
        Func<EventWrittenEventArgs, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            Task eventWritten;
            lock (_syncRoot)
            {
                if (_events.Any(predicate))
                {
                    return;
                }

                eventWritten = _eventWritten.Task;
            }

            await eventWritten.WaitAsync(cancellationToken);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        TaskCompletionSource eventWritten;
        lock (_syncRoot)
        {
            _events.Add(eventData);
            eventWritten = _eventWritten;
            _eventWritten = CreateEventSource();
        }

        eventWritten.SetResult();
    }

    private static TaskCompletionSource CreateEventSource()
    {
        return new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
