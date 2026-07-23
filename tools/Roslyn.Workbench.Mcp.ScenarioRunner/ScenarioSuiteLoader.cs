using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal static class ScenarioSuiteLoader
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<ScenarioSuite> LoadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "scenario-suite.json");
        await using var stream = File.OpenRead(path);
        var suite = await JsonSerializer.DeserializeAsync<ScenarioSuite>(stream, _serializerOptions, cancellationToken);

        return suite ?? throw new InvalidDataException($"Scenario suite '{path}' did not contain a suite definition.");
    }
}
