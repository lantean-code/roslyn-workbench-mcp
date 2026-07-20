namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record ProfileInvocationTiming
{
    public required double MedianMilliseconds { get; init; }

    public required double P95Milliseconds { get; init; }

    public static ProfileInvocationTiming Create(IReadOnlyList<double> elapsedMilliseconds)
    {
        var orderedValues = elapsedMilliseconds.Order().ToArray();
        return new ProfileInvocationTiming
        {
            MedianMilliseconds = Percentile(orderedValues, 0.5),
            P95Milliseconds = Percentile(orderedValues, 0.95),
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
