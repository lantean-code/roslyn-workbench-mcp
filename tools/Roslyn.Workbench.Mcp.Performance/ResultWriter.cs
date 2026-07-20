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
