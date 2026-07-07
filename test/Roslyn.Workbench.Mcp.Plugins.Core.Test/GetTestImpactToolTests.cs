using System.Text.Json;

using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetTestImpactToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingTestImpactByDefault_THEN_ShouldOmitReasonBranch()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        plugin.Register(registry);

        var result = await PluginToolTestHarness.InvokeAsync<JsonElement>(coordinator, registry, "get-test-impact", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FormatterCaller.Call",
            }),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.GetProperty("tests").GetProperty("items").EnumerateArray().All(static test => !test.TryGetProperty("reasons", out _)).Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingTestImpactReasonsExplicitly_THEN_ShouldIncludeReasonBranch()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        plugin.Register(registry);

        var result = await PluginToolTestHarness.InvokeAsync<JsonElement>(coordinator, registry, "get-test-impact", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FormatterCaller.Call",
            }),
            ["includeReasons"] = JsonSerializer.SerializeToElement(true),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.GetProperty("tests").GetProperty("items").EnumerateArray().Select(static test => test.GetProperty("reasons").EnumerateArray().Select(static reason => reason.GetString()).ToArray()).Should().Contain(static reasons =>
            reasons.Any(static reason => reason!.Contains("reference", StringComparison.OrdinalIgnoreCase) || reason.Contains("call", StringComparison.OrdinalIgnoreCase)));
    }

}
