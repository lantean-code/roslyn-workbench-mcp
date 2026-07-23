using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum CommitCancellationBoundary
{
    BeforeApplying,
    AfterApplying,
}
