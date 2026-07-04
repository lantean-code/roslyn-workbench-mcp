namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetApiSurfaceToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnExportedSymbols()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetApiSurfaceTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-api-surface", target, new GetApiSurfaceRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbols.Should().Contain(static symbol => symbol.Symbol!.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        result.Data.Symbols.Should().Contain(static symbol => symbol.Symbol!.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
    }
}
