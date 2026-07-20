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
        var traceEvent = listener.Events.Should().ContainSingle().Which;
        traceEvent.EventName.Should().Be("PhaseCompleted");
        traceEvent.PayloadNames.Should().Equal("elapsedMilliseconds", "operation", "phase");
        traceEvent.Payload.Should().NotBeNull();
        var payload = traceEvent.Payload ?? [];
        payload[0].Should().BeOfType<double>();
        payload[1].Should().Be("Operation");
        payload[2].Should().Be("Phase");
    }
}
