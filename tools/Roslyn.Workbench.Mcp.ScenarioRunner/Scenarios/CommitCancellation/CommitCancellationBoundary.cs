using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CommitCancellation;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum CommitCancellationBoundary
{
    BeforeApplying,
    AfterApplying,
}
