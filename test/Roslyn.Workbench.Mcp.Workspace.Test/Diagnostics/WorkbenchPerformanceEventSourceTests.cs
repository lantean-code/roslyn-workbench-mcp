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

    [Fact]
    public void GIVEN_EnabledListener_WHEN_AtomicCommitRetryIsRecorded_THEN_ShouldWriteCountsWithoutPathData()
    {
        var eventSource = WorkbenchPerformanceEventSource.Log;
        using var listener = new WorkbenchPerformanceEventListener(eventSource);

        eventSource.AtomicFileCommitRetry(retryNumber: 2, delayMilliseconds: 50);

        eventSource.ConstructionException.Should().BeNull();
        var retryEvent = listener.Events.Should().ContainSingle(item => item.EventName == "AtomicFileCommitRetry").Which;
        retryEvent.PayloadNames.Should().Equal("retryNumber", "delayMilliseconds");
        retryEvent.Payload.Should().Equal(2, 50);
    }

    [Fact]
    public void GIVEN_EnabledListener_WHEN_InputMonitorIsConfigured_THEN_ShouldWriteBoundedResourceCounts()
    {
        var eventSource = WorkbenchPerformanceEventSource.Log;
        using var listener = new WorkbenchPerformanceEventListener(eventSource);

        eventSource.WorkspaceInputMonitorConfigured(
            externalRootCount: 2,
            evaluatedGlobCount: 4,
            externalWatcherCount: 1);

        eventSource.ConstructionException.Should().BeNull();
        var configurationEvent = listener.Events.Should().ContainSingle(
            item => item.EventName == "WorkspaceInputMonitorConfigured").Which;

        configurationEvent.PayloadNames.Should().Equal(
            "externalRootCount",
            "evaluatedGlobCount",
            "externalWatcherCount");

        configurationEvent.Payload.Should().Equal(2, 4, 1);
    }

    private static bool IsExpectedTraceEvent(EventWrittenEventArgs traceEvent)
    {
        return traceEvent.EventName == "PhaseCompleted"
            && traceEvent.Payload is { Count: 3 }
            && Equals(traceEvent.Payload[1], "Operation")
            && Equals(traceEvent.Payload[2], "Phase");
    }
}
