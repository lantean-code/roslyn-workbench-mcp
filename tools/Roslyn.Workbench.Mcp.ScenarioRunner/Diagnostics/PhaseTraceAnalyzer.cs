using Microsoft.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

internal static class PhaseTraceAnalyzer
{
    private const string _performanceProviderName = "Roslyn-Workbench-Mcp";
    private const string _phaseCompletedEventName = "PhaseCompleted";
    private const string _cacheMetricEventName = "CacheMetric";
    private const string _atomicFileCommitRetryEventName = "AtomicFileCommitRetry";
    private const string _toolTotalPhase = "tool-total";

    public static IReadOnlyList<PhaseTraceSummary> Analyze(string tracePath)
    {
        var observations = ReadObservations(tracePath);
        if (observations.Count == 0)
        {
            throw new InvalidDataException(
                $"Trace '{tracePath}' contains no '{_performanceProviderName}' phase events.");
        }

        var toolTotalMedians = GetToolTotalMedians(observations);
        return observations
            .GroupBy(static item => (item.Operation, item.Phase))
            .OrderBy(static group => group.Key.Operation, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.Phase, StringComparer.Ordinal)
            .Select(group => CreateSummary(
                group,
                toolTotalMedians.GetValueOrDefault(group.Key.Operation)))
            .ToArray();
    }

    public static IReadOnlyList<CacheMetricSummary> AnalyzeCacheMetrics(string tracePath)
    {
        var observations = new List<CacheMetricObservation>();
        using var source = new EventPipeEventSource(tracePath);
        source.Dynamic.All += traceEvent =>
        {
            if (!string.Equals(traceEvent.ProviderName, _performanceProviderName, StringComparison.Ordinal)
                || !string.Equals(traceEvent.EventName, _cacheMetricEventName, StringComparison.Ordinal)
                || traceEvent.PayloadByName("family") is not string family
                || traceEvent.PayloadByName("metric") is not string metric
                || traceEvent.PayloadByName("value") is not long value)
            {
                return;
            }

            var observation = new CacheMetricObservation
            {
                Family = family,
                Metric = metric,
                Value = value,
            };

            observations.Add(observation);
        };

        source.Process();
        var summaries = observations
            .GroupBy(static item => (item.Family, item.Metric))
            .OrderBy(static group => group.Key.Family, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.Metric, StringComparer.Ordinal)
            .Select(CreateCacheMetricSummary)
            .ToArray();

        return summaries;
    }

    public static AtomicFileCommitRetrySummary AnalyzeAtomicFileCommitRetries(string tracePath)
    {
        var observations = new List<AtomicFileCommitRetryObservation>();
        using var source = new EventPipeEventSource(tracePath);
        source.Dynamic.All += traceEvent =>
        {
            if (!string.Equals(traceEvent.ProviderName, _performanceProviderName, StringComparison.Ordinal)
                || !string.Equals(traceEvent.EventName, _atomicFileCommitRetryEventName, StringComparison.Ordinal)
                || traceEvent.PayloadByName("retryNumber") is not int retryNumber
                || traceEvent.PayloadByName("delayMilliseconds") is not int delayMilliseconds)
            {
                return;
            }

            var observation = new AtomicFileCommitRetryObservation
            {
                RetryNumber = retryNumber,
                DelayMilliseconds = delayMilliseconds,
            };

            observations.Add(observation);
        };

        source.Process();
        return new AtomicFileCommitRetrySummary
        {
            TotalRetryAttempts = observations.Count,
            RetriedOperationCount = observations.Count(static item => item.RetryNumber == 1),
            MaximumRetriesForOneOperation = observations.Count == 0
                ? 0
                : observations.Max(static item => item.RetryNumber),
            TotalDelayMilliseconds = observations.Sum(static item => item.DelayMilliseconds),
        };
    }

    private static CacheMetricSummary CreateCacheMetricSummary(
        IGrouping<(string Family, string Metric), CacheMetricObservation> group)
    {
        var value = IsMaximumMetric(group.Key.Metric)
            ? group.Max(static item => item.Value)
            : group.Sum(static item => item.Value);

        var summary = new CacheMetricSummary
        {
            Family = group.Key.Family,
            Metric = group.Key.Metric,
            Value = value,
        };

        return summary;
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
            var elapsed = traceEvent.PayloadByName("elapsedMilliseconds");
            if (traceEvent.PayloadByName("operation") is not string operation || traceEvent.PayloadByName("phase") is not string phase || elapsed is not double elapsedMilliseconds)
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

    private static Dictionary<string, double> GetToolTotalMedians(
        IReadOnlyList<PhaseTraceObservation> observations)
    {
        return observations
            .Where(static item => string.Equals(item.Phase, _toolTotalPhase, StringComparison.Ordinal))
            .GroupBy(static item => item.Operation, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => Percentile(
                    group
                        .Select(static item => item.ElapsedMilliseconds)
                        .Order()
                        .ToArray(),
                    0.5),
                StringComparer.Ordinal);
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

    private static bool IsMaximumMetric(string metric)
    {
        return metric.StartsWith("peak-", StringComparison.Ordinal)
            || metric.StartsWith("largest-", StringComparison.Ordinal);
    }
}
