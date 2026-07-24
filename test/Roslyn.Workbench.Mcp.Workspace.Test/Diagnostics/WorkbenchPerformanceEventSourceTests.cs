using System.Diagnostics.Tracing;
using Roslyn.Workbench.Mcp.Workspace.Diagnostics;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Diagnostics;

public sealed class WorkbenchPerformanceEventSourceTests
{
    [Fact]
    public void GIVEN_EnabledListener_WHEN_CompletingPhase_THEN_ShouldWriteStructuredTimingEvent()
    {
        var eventSource = WorkbenchPerformanceEventSource.Log;
        using var listener = new WorkbenchPerformanceEventListener(eventSource);

        using (eventSource.StartPhase("Operation", "Phase"))
        {
        }

        eventSource.ConstructionException.Should().BeNull();
        var matchingEvents = listener.Events
            .Where(IsExpectedTraceEvent)
            .ToArray();

        var traceEvent = matchingEvents.Should().ContainSingle().Which;
        traceEvent.EventName.Should().Be("PhaseCompleted");
        traceEvent.PayloadNames.Should().Equal("elapsedMilliseconds", "operation", "phase");
        traceEvent.Payload.Should().NotBeNull();
        var payload = traceEvent.Payload ?? [];
        payload[0].Should().BeOfType<double>();
        payload[1].Should().Be("Operation");
        payload[2].Should().Be("Phase");
    }

    private static bool IsExpectedTraceEvent(EventWrittenEventArgs traceEvent)
    {
        return traceEvent.EventName == "PhaseCompleted"
            && traceEvent.Payload is { Count: 3 }
            && Equals(traceEvent.Payload[1], "Operation")
            && Equals(traceEvent.Payload[2], "Phase");
    }
}
