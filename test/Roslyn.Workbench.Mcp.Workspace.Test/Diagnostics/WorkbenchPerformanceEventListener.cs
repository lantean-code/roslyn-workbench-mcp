using System.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Diagnostics;

internal sealed class WorkbenchPerformanceEventListener : EventListener
{
    private readonly List<EventWrittenEventArgs> _events = [];
    private readonly Lock _syncRoot = new();

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

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        lock (_syncRoot)
        {
            _events.Add(eventData);
        }
    }
}
