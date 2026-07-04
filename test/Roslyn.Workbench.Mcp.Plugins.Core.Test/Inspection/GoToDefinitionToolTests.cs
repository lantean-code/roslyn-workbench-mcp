namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GoToDefinitionToolTests
{
    [Fact]
    public async Task GIVEN_MetadataSymbol_WHEN_ExecutingTool_THEN_ShouldReturnMetadataDefinition()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var resolveTarget = new ResolveSymbolTool();
        var target = new GoToDefinitionTool();

        var resolve = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "resolve-symbol", resolveTarget, new ResolveSymbolRequest
        {
            Location = fixture.GetLocation("ToUpperInvariant"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });
        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "go-to-definition", target, new GoToDefinitionRequest
        {
            Symbol = resolve.Data!.Selector!,
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Definitions.Should().ContainSingle(static location => location.IsMetadata);
    }
}
