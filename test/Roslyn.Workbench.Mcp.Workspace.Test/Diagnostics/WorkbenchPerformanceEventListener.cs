using System.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Diagnostics;

internal sealed class WorkbenchPerformanceEventListener : EventListener
{
    private readonly List<EventWrittenEventArgs> _events = [];

    public IReadOnlyList<EventWrittenEventArgs> Events => _events;

    public WorkbenchPerformanceEventListener(EventSource eventSource)
    {
        EnableEvents(eventSource, EventLevel.Informational);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        _events.Add(eventData);
    }
}
