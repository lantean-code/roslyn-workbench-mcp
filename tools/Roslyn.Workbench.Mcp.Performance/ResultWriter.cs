using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Performance;

internal static class ResultWriter
{
    private static readonly JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

    public static async Task WriteMeasurementsAsync(
        string outputDirectory,
        IReadOnlyList<ScenarioRunResult> results,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "measurements.json");
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(stream, results, _serializerOptions, cancellationToken);
        }

        var markdown = CreateSummary(results);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "summary.md"),
            markdown,
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteProfileAsync(
        string outputDirectory,
        ProfileRunResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "profile.json");
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, result, _serializerOptions, cancellationToken);
    }

    public static async Task WritePhaseSummaryAsync(
        string outputDirectory,
        IReadOnlyList<PhaseTraceSummary> phases,
        int invocationCount,
        ProfileInvocationTiming? invocationTiming,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench phase summary")
            .AppendLine()
            .Append("Profiled invocations: ").AppendLine(invocationCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine()
            .AppendLine("Phase timings are Host-internal elapsed durations captured only while the custom EventSource provider is enabled. Nested phases overlap their parent and must not be added together.")
            .AppendLine();

        AppendInvocationReconciliation(builder, phases, invocationTiming);

        builder
            .AppendLine("| Operation | Phase | Count | Median (ms) | P95 (ms) | Total (ms) | Median share of tool total |")
            .AppendLine("|---|---|---:|---:|---:|---:|---:|");

        foreach (var phase in phases)
        {
            builder
                .Append("| ").Append(phase.Operation)
                .Append(" | ").Append(phase.Phase)
                .Append(" | ").Append(phase.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.MedianMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(phase.MedianToolSharePercent.ToString("F1", CultureInfo.InvariantCulture))
                .AppendLine("% |");
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "phases.md"),
            builder.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    public static async Task WriteRunValidationAsync(
        string outputDirectory,
        RunValidationResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "validation.json");
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, result, _serializerOptions, cancellationToken);
    }

    private static void AppendInvocationReconciliation(
        StringBuilder builder,
        IReadOnlyList<PhaseTraceSummary> phases,
        ProfileInvocationTiming? invocationTiming)
    {
        if (invocationTiming is null)
        {
            return;
        }

        var toolTotal = phases.SingleOrDefault(static phase => phase.Phase == "tool-total");
        if (toolTotal is null)
        {
            return;
        }

        var uninstrumentedMedian = Math.Max(
            0,
            invocationTiming.MedianMilliseconds - toolTotal.MedianMilliseconds);

        builder
            .AppendLine("| Boundary | Median (ms) | P95 (ms) |")
            .AppendLine("|---|---:|---:|")
            .Append("| End-to-end MCP invocation | ")
            .Append(invocationTiming.MedianMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(invocationTiming.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Instrumented Host tool | ")
            .Append(toolTotal.MedianMilliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .Append(" | ")
            .Append(toolTotal.P95Milliseconds.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" |")
            .Append("| Outside instrumented Host tool | ")
            .Append(uninstrumentedMedian.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine(" | — |")
            .AppendLine();
    }

    private static string CreateSummary(IReadOnlyList<ScenarioRunResult> results)
    {
        var builder = new StringBuilder()
            .AppendLine("# Roslyn Workbench performance summary")
            .AppendLine()
            .AppendLine("| Repository | Size | Scenario | Tool | Median elapsed (ms) | P95 elapsed (ms) | Median host CPU (ms) | Max working set (MiB) | Response (KiB) |")
            .AppendLine("|---|---:|---|---|---:|---:|---:|---:|---:|");

        foreach (var result in results)
        {
            var elapsed = result.Measurements.Select(static item => item.ElapsedMilliseconds).Order().ToArray();
            var cpu = result.Measurements.Select(static item => item.HostCpuMilliseconds).Order().ToArray();
            var maxWorkingSet = result.Measurements.Max(static item => item.WorkingSetBytes);
            var responseBytes = result.Measurements.Max(static item => item.ResponseBytes);

            builder
                .Append("| ").Append(result.Repository)
                .Append(" | ").Append(result.RepositorySize)
                .Append(" | ").Append(result.Scenario)
                .Append(" | ").Append(result.Tool)
                .Append(" | ").Append(Percentile(elapsed, 0.5).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(Percentile(elapsed, 0.95).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(Percentile(cpu, 0.5).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append((maxWorkingSet / 1024d / 1024d).ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append((responseBytes / 1024d).ToString("F2", CultureInfo.InvariantCulture))
                .AppendLine(" |");
        }

        return builder.ToString();
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

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
