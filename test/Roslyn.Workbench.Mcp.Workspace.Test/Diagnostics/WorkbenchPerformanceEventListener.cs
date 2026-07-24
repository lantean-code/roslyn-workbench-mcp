using System.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Diagnostics;

internal sealed class WorkbenchPerformanceEventListener : EventListener
{
    private readonly List<EventWrittenEventArgs> _events = [];
    private readonly Lock _sync = new();

    public IReadOnlyList<EventWrittenEventArgs> Events
    {
        get
        {
            lock (_sync)
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
        lock (_sync)
        {
            _events.Add(eventData);
        }
    }
}
