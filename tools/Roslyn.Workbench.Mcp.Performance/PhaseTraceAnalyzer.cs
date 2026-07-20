using Microsoft.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Performance;

internal static class PhaseTraceAnalyzer
{
    private const string _performanceProviderName = "Roslyn-Workbench-Mcp";
    private const string _phaseCompletedEventName = "PhaseCompleted";
    private const string _toolTotalPhase = "tool-total";

    public static IReadOnlyList<PhaseTraceSummary> Analyze(string tracePath)
    {
        var observations = ReadObservations(tracePath);
        if (observations.Count == 0)
        {
            throw new InvalidDataException(
                $"Trace '{tracePath}' contains no '{_performanceProviderName}' phase events.");
        }

        var toolTotalMedian = GetToolTotalMedian(observations);
        return observations
            .GroupBy(static item => (item.Operation, item.Phase))
            .OrderBy(static group => group.Key.Operation, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.Phase, StringComparer.Ordinal)
            .Select(group => CreateSummary(group, toolTotalMedian))
            .ToArray();
    }

    private static List<PhaseTraceObservation> ReadObservations(string tracePath)
    {
        var observations = new List<PhaseTraceObservation>();
        using var source = new EventPipeEventSource(tracePath);
        source.Dynamic.All += traceEvent =>
        {
            if (!string.Equals(traceEvent.ProviderName, _performanceProviderName, StringComparison.Ordinal)
                || !string.Equals(traceEvent.EventName, _phaseCompletedEventName, StringComparison.Ordinal))
            {
                return;
            }

            var operation = traceEvent.PayloadByName("operation") as string;
            var phase = traceEvent.PayloadByName("phase") as string;
            var elapsed = traceEvent.PayloadByName("elapsedMilliseconds");
            if (operation is null || phase is null || elapsed is not double elapsedMilliseconds)
            {
                return;
            }

            observations.Add(new PhaseTraceObservation
            {
                Operation = operation,
                Phase = phase,
                ElapsedMilliseconds = elapsedMilliseconds,
            });
        };

        source.Process();
        return observations;
    }

    private static double GetToolTotalMedian(IReadOnlyList<PhaseTraceObservation> observations)
    {
        var toolTotals = observations
            .Where(static item => string.Equals(item.Phase, _toolTotalPhase, StringComparison.Ordinal))
            .Select(static item => item.ElapsedMilliseconds)
            .Order()
            .ToArray();

        return Percentile(toolTotals, 0.5);
    }

    private static PhaseTraceSummary CreateSummary(
        IEnumerable<PhaseTraceObservation> observations,
        double toolTotalMedian)
    {
        var values = observations
            .Select(static item => item.ElapsedMilliseconds)
            .Order()
            .ToArray();

        var first = observations.First();
        var median = Percentile(values, 0.5);
        return new PhaseTraceSummary
        {
            Operation = first.Operation,
            Phase = first.Phase,
            Count = values.Length,
            MedianMilliseconds = median,
            P95Milliseconds = Percentile(values, 0.95),
            TotalMilliseconds = values.Sum(),
            MedianToolSharePercent = toolTotalMedian <= 0
                ? 0
                : median / toolTotalMedian * 100,
        };
    }

    private static double Percentile(double[] orderedValues, double percentile)
    {
        if (orderedValues.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * orderedValues.Length) - 1;
        return orderedValues[Math.Max(0, index)];
    }
}
