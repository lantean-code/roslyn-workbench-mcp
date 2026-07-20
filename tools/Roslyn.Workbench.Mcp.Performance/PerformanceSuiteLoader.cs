using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Performance;

internal static class PerformanceSuiteLoader
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<PerformanceSuite> LoadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "performance-suite.json");
        await using var stream = File.OpenRead(path);
        var suite = await JsonSerializer.DeserializeAsync<PerformanceSuite>(stream, _serializerOptions, cancellationToken);

        return suite ?? throw new InvalidDataException($"Performance suite '{path}' did not contain a suite definition.");
    }
}
